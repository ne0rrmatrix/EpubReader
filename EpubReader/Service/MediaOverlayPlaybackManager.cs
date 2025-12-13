using System.Text.Encodings.Web;
using System.Text.Json;
using EpubReader.Models;
using EpubReader.Models.MediaOverlays;
using Plugin.Maui.Audio;

namespace EpubReader.Service;

/// <summary>
/// Coordinates media overlay playback, highlighting, and UI updates for the reader surface.
/// </summary>
public sealed class MediaOverlayPlaybackManager : IDisposable
{
	readonly BookViewModel viewModel;
	Book book;
	readonly WebView webView;

	readonly IDispatcher dispatcher;
	readonly JsonSerializerOptions serializerOptions = new()
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};
	readonly Dictionary<string, MediaOverlayAudioResource> audioCache = new(StringComparer.OrdinalIgnoreCase);
	List<MediaOverlaySegment> segments = [];
	int currentChapterIndex = -1;
	int segmentIndex;
	bool isEnabled;
	bool isPlaying;
	bool isUiBootstrapped;
	bool isWebViewReady;
	bool isReaderModeHidden;
	double? currentChapterDurationSeconds;
	bool disposed;


	readonly AudioPlaybackService audioPlaybackService;
	IDispatcherTimer? clipTimer;
	TimeSpan currentClipBegin = TimeSpan.Zero;
	TimeSpan? currentClipEnd;
	string? currentAudioResourceId;
	bool isSeekPending;
	int? lastReportedSecond;
	double? pendingSeekOverrideSeconds;
	double? restoredPositionSeconds;
	MediaOverlayPlaybackProgress? pendingRestore;
	bool isApplyingRestore;

	public event EventHandler<MediaOverlayPlaybackProgress>? PlaybackProgressChanged;

	string ActiveClass => string.IsNullOrWhiteSpace(book.MediaOverlayActiveClass)
		? "-epub-media-overlay-active"
		: book.MediaOverlayActiveClass!;

	string PlaybackClass => string.IsNullOrWhiteSpace(book.MediaOverlayPlaybackActiveClass)
		? "-epub-media-overlay-playing"
		: book.MediaOverlayPlaybackActiveClass!;

	public bool IsSupported => book.HasNarratedMedia;

	public MediaOverlayPlaybackManager(BookViewModel viewModel, WebView webView, IAudioManager audioManager)
	{
		this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		this.webView = webView ?? throw new ArgumentNullException(nameof(webView));
		_ = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
		book = viewModel.Book ?? new Book();
		dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException("Dispatcher unavailable.");
		UpdateBook(book);
		audioPlaybackService = new AudioPlaybackService(audioManager);
		audioPlaybackService.PlaybackEnded += OnPlaybackEnded;
	}

	public void UpdateBook(Book newBook)
	{
		book = newBook ?? throw new ArgumentNullException(nameof(newBook));
		audioCache.Clear();
		foreach (var resource in book.MediaOverlayAudio.DistinctBy(res => res.NormalizedPath, StringComparer.OrdinalIgnoreCase))
		{
			audioCache[resource.NormalizedPath] = resource;
		}
	}

	public async Task InitializeUiAsync()
	{
		if (!IsSupported)
		{
			System.Diagnostics.Debug.WriteLine("Media overlay playback not supported for this book; skipping UI initialization.");
			await InvokeScriptAsync("setMediaOverlayVisibility", false).ConfigureAwait(false);
			return;
		}

		var tasks = new List<Task>
		{
			InvokeScriptAsync("setMediaOverlayVisibility", true),
			InvokeScriptAsync("setMediaOverlayReaderModeHidden", isReaderModeHidden)
		};

		if (!isUiBootstrapped)
		{
			isUiBootstrapped = true;
			tasks.Add(InvokeScriptAsync("initializeMediaOverlayUi", new
			{
				enabled = isEnabled,
				narrator = book.MediaOverlayNarrator,
				durationSeconds = GetActiveDurationSeconds(),
				chapterTitle = GetCurrentChapterTitle(),
				segmentCount = segments.Count
			}));
			System.Diagnostics.Debug.WriteLine("Media overlay UI initialized.");
		}
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	public async Task SetReaderModeHiddenAsync(bool hidden)
	{
		isReaderModeHidden = hidden;

		if (!IsSupported || !isWebViewReady)
		{
			return;
		}

		await InvokeScriptAsync("setMediaOverlayReaderModeHidden", hidden).ConfigureAwait(false);
	}

	public async Task OnChapterRequestedAsync(int chapterIndex)
	{
		if (!IsSupported)
		{
			return;
		}

		isWebViewReady = false;
		await UpdateChapterContextAsync(chapterIndex);
		StopPlaybackInternal();
	}

	public async Task OnPageLoadedAsync(int chapterIndex)
	{
		if (!IsSupported)
		{
			return;
		}

		isWebViewReady = true;
		await InitializeUiAsync().ConfigureAwait(false);

		if (currentChapterIndex != chapterIndex)
		{
			await UpdateChapterContextAsync(chapterIndex).ConfigureAwait(false);
		}

		await ApplyPendingRestoreAsync().ConfigureAwait(false);

		if (isEnabled)
		{
			await HighlightCurrentSegmentAsync().ConfigureAwait(false);
		}

		await UpdateUiStateAsync().ConfigureAwait(false);
	}

	public void SetPendingRestore(MediaOverlayPlaybackProgress? progress)
	{
		pendingRestore = progress;
	}

	public MediaOverlayPlaybackProgress? GetPlaybackProgressSnapshot()
	{
		if (!IsSupported)
		{
			return null;
		}

		var chapterIndex = currentChapterIndex >= 0 ? currentChapterIndex : book.CurrentChapter;
		var fragmentId = segments.Count > 0 && segmentIndex >= 0 && segmentIndex < segments.Count
			? segments[segmentIndex].FragmentId
			: null;
		var position = GetCurrentPositionSeconds() ?? restoredPositionSeconds;
		return new MediaOverlayPlaybackProgress(
			Enabled: isEnabled,
			ChapterIndex: chapterIndex,
			SegmentIndex: Math.Max(0, segmentIndex),
			PositionSeconds: position,
			FragmentId: fragmentId);
	}

	async Task ApplyPendingRestoreAsync()
	{
		if (!IsSupported || !isWebViewReady)
		{
			return;
		}

		var restore = pendingRestore;
		if (restore is null)
		{
			return;
		}

		if (currentChapterIndex >= 0 && restore.ChapterIndex != currentChapterIndex)
		{
			// Wait until the intended chapter is active.
			return;
		}

		pendingRestore = null;
		isApplyingRestore = true;
		try
		{
			isEnabled = restore.Enabled;
			isPlaying = false;

			// Avoid auto-play when restoring.
			StopPlaybackInternal(disposeSession: true, pauseOnly: false);

			if (segments.Count > 0)
			{
				if (restore.PositionSeconds is double pos)
				{
					if (!TryApplyRestoredPosition(pos))
					{
						ApplyRestoredAnchor(restore);
					}
				}
				else
				{
					ApplyRestoredAnchor(restore);
				}
			}
			else
			{
				restoredPositionSeconds = restore.PositionSeconds;
				pendingSeekOverrideSeconds = null;
			}

			if (isEnabled)
			{
				await HighlightCurrentSegmentAsync().ConfigureAwait(false);
			}
			else
			{
				await ClearHighlightAsync().ConfigureAwait(false);
			}
			await UpdateUiStateAsync().ConfigureAwait(false);
		}
		finally
		{
			isApplyingRestore = false;
		}
	}

	void ApplyRestoredAnchor(MediaOverlayPlaybackProgress restore)
	{
		var target = restore.SegmentIndex;
		if (!string.IsNullOrWhiteSpace(restore.FragmentId))
		{
			var idx = segments.FindIndex(seg => string.Equals(seg.FragmentId, restore.FragmentId, StringComparison.Ordinal));
			if (idx >= 0)
			{
				target = idx;
			}
		}
		segmentIndex = Math.Clamp(target, 0, Math.Max(0, segments.Count - 1));
		pendingSeekOverrideSeconds = segments[segmentIndex].Node.Audio?.ClipBegin?.TotalSeconds ?? 0;
		restoredPositionSeconds = restore.PositionSeconds;
	}

	bool TryApplyRestoredPosition(double positionSeconds)
	{
		var total = GetActiveDurationSeconds();
		if (total is not null)
		{
			positionSeconds = Math.Clamp(positionSeconds, 0, total.Value);
		}

		var hasAnyDuration = false;
		var mapped = false;
		double cumulative = 0;
		for (int i = 0; i < segments.Count; i++)
		{
			var audio = segments[i].Node.Audio;
			if (audio?.ClipBegin is null || audio.ClipEnd is null)
			{
				continue;
			}

			hasAnyDuration = true;

			var segLen = (audio.ClipEnd.Value - (audio.ClipBegin ?? TimeSpan.Zero)).TotalSeconds;
			if (positionSeconds < cumulative + segLen)
			{
				segmentIndex = i;
				var offsetInSeg = Math.Max(0, positionSeconds - cumulative);
				pendingSeekOverrideSeconds = (audio.ClipBegin ?? TimeSpan.Zero).TotalSeconds + offsetInSeg;
				restoredPositionSeconds = positionSeconds;
				mapped = true;
				break;
			}
			cumulative += segLen;
		}

		if (!hasAnyDuration)
		{
			// Can't map position to a clip without durations.
			return false;
		}

		if (!mapped)
		{
			// Position beyond known durations; best-effort: clamp to end.
			segmentIndex = Math.Clamp(segments.Count - 1, 0, Math.Max(0, segments.Count - 1));
			pendingSeekOverrideSeconds = segments[segmentIndex].Node.Audio?.ClipBegin?.TotalSeconds ?? 0;
			restoredPositionSeconds = positionSeconds;
		}

		return true;
	}

	void RaisePlaybackProgressChanged()
	{
		if (PlaybackProgressChanged is null || isApplyingRestore)
		{
			return;
		}

		var snapshot = GetPlaybackProgressSnapshot();
		if (snapshot is null)
		{
			return;
		}

		try
		{
			PlaybackProgressChanged?.Invoke(this, snapshot);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Trace.TraceWarning($"Media overlay progress callback failed: {ex.Message}");
		}
	}

	public async Task SetEnabledAsync(bool enabled)
	{
		if (!IsSupported)
		{
			await viewModel.ShowInfoToastAsync("This title does not include narrated overlays.").ConfigureAwait(false);
			return;
		}

		if (isEnabled == enabled)
		{
			System.Diagnostics.Debug.WriteLine("Media overlay enabled state is already set to the requested value; no change made.");
			await UpdateUiStateAsync().ConfigureAwait(false);
			return;
		}

		isEnabled = enabled;

		if (!isEnabled)
		{
			await StopAsync();
		}
		else if (segments.Count == 0)
		{
			await viewModel.ShowInfoToastAsync("Audio isn't available for this section.").ConfigureAwait(false);
		}
		else if (isWebViewReady)
		{
			await HighlightCurrentSegmentAsync().ConfigureAwait(false);
		}

		await UpdateUiStateAsync().ConfigureAwait(false);
	}

	public async Task PlayAsync()
	{
		if (!IsSupported)
		{
			await viewModel.ShowInfoToastAsync("This title does not include narrated overlays.").ConfigureAwait(false);
			return;
		}

		if (!isEnabled)
		{
			await SetEnabledAsync(true).ConfigureAwait(false);
			return;
		}

		if (segments.Count == 0)
		{
			await viewModel.ShowInfoToastAsync("Audio isn't available for this section.").ConfigureAwait(false);
			return;
		}

		// If the saved/restored segment isn't visible on the current page, prefer
		// the first visible segment on the page (and make that the new saved segment).
		var allFragmentIds = segments.Select(s => s.FragmentId).ToList();
		// If the current segmentIndex points at a valid segment, check visibility.
		if (segmentIndex >= 0 && segmentIndex < segments.Count)
		{
			var (visibleIndex, _) = await GetVisibleSegmentPositionAsync(segments[segmentIndex].FragmentId, allFragmentIds).ConfigureAwait(false);
			if (visibleIndex < 0)
			{
				var firstVisible = await FindFirstVisibleSegmentIndexAsync(allFragmentIds).ConfigureAwait(false);
				if (firstVisible >= 0)
				{
					segmentIndex = firstVisible;
					pendingSeekOverrideSeconds = segments[segmentIndex].Node.Audio?.ClipBegin?.TotalSeconds ?? 0;
				}
			}
		}
		else
		{
			// No saved segment; pick the first visible one on the page if any.
			var firstVisible = await FindFirstVisibleSegmentIndexAsync(allFragmentIds).ConfigureAwait(false);
			if (firstVisible >= 0)
			{
				segmentIndex = firstVisible;
				pendingSeekOverrideSeconds = segments[segmentIndex].Node.Audio?.ClipBegin?.TotalSeconds ?? 0;
			}
		}

		segmentIndex = Math.Clamp(segmentIndex, 0, segments.Count - 1);
		// If a restore (or seek) provided an explicit position, ensure playback begins at it.
		var forceSeek = pendingSeekOverrideSeconds is not null;
		await StartSegmentAsync(segments[segmentIndex], forceSeek: forceSeek).ConfigureAwait(false);
	}

	public async Task PauseAsync()
	{
		if (!isPlaying)
		{
			return;
		}

		StopPlaybackInternal(disposeSession: false, pauseOnly: true);
		await UpdateUiStateAsync().ConfigureAwait(false);
	}

	public async Task NextAsync()
	{
		if (!IsSupported || segments.Count == 0)
		{
			return;
		}


		if (segmentIndex < segments.Count - 1)
		{
			segmentIndex++;
			if (isEnabled && isPlaying)
			{
				await StartSegmentAsync(segments[segmentIndex], forceSeek: true).ConfigureAwait(false);
			}
			else
			{
				await HighlightCurrentSegmentAsync().ConfigureAwait(false);
				await UpdateUiStateAsync().ConfigureAwait(false);
			}
		}
		else
		{
			await FinishDocumentAsync().ConfigureAwait(false);
		}
	}

	public async Task PreviousAsync()
	{
		if (!IsSupported || segments.Count == 0)
		{
			return;
		}


		if (segmentIndex > 0)
		{
			segmentIndex--;
			if (isEnabled && isPlaying)
			{
				await StartSegmentAsync(segments[segmentIndex], forceSeek: true, preferPreviousPage: true).ConfigureAwait(false);
			}
			else
			{
				await HighlightCurrentSegmentAsync(preferPreviousPage: true).ConfigureAwait(false);
				await UpdateUiStateAsync().ConfigureAwait(false);
			}
		}
		else
		{
			await viewModel.ShowInfoToastAsync("Already at the first narrated segment.").ConfigureAwait(false);
		}
	}

	public async Task StopAsync()
	{

		StopPlaybackInternal();
		await ClearHighlightAsync().ConfigureAwait(false);
		await UpdateUiStateAsync().ConfigureAwait(false);
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		StopPlaybackInternal();
		clipTimer = null;
		audioPlaybackService.Dispose();
	}

	async Task UpdateChapterContextAsync(int chapterIndex)
	{
		currentChapterIndex = chapterIndex;
		var chapter = book.Chapters.ElementAtOrDefault(chapterIndex);
		if (chapter is null)
		{
			segments = [];
			currentChapterDurationSeconds = null;
			return;
		}

		var document = ResolveDocumentForChapter(chapter.FileName);
		segments = BuildSegments(document, chapter.FileName);
		currentChapterDurationSeconds = CalculateDurationSeconds(segments);
		segmentIndex = 0;

		if (isEnabled && segments.Count == 0)
		{
			await viewModel.ShowInfoToastAsync("Audio isn't available for this section.").ConfigureAwait(false);
		}
	}

	MediaOverlayDocument? ResolveDocumentForChapter(string chapterFileName)
	{
		return book.MediaOverlays.FirstOrDefault(document =>
			document.AssociatedContentDocuments.Any(content =>
				string.Equals(MediaOverlayPathHelper.ExtractFileName(content), chapterFileName, StringComparison.OrdinalIgnoreCase)));
	}

	static List<MediaOverlaySegment> BuildSegments(MediaOverlayDocument? document, string chapterFileName)
	{
		if (document is null)
		{
			return [];
		}

		var fileName = MediaOverlayPathHelper.ExtractFileName(chapterFileName);
		var list = new List<MediaOverlaySegment>();

		foreach (var node in document.FlattenedNodes)
		{
			if (node.Text is null || node.Audio is null)
			{
				continue;
			}

			var (sourcePath, fragment) = MediaOverlayPathHelper.SplitSource(node.Text.Source);
			if (string.IsNullOrWhiteSpace(fragment))
			{
				continue;
			}

			var sourceFileName = MediaOverlayPathHelper.ExtractFileName(sourcePath);
			if (!string.Equals(sourceFileName, fileName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			list.Add(new MediaOverlaySegment(node, fragment));
		}

		return list;
	}

	async Task StartSegmentAsync(MediaOverlaySegment segment, bool forceSeek = false, bool preferPreviousPage = false)
	{
		if (!TryGetAudioResource(segment.Node.Audio?.Source, out var resource))
		{
			await HandleMissingAudioResourceAsync().ConfigureAwait(false);
			return;
		}

		var normalizedResourceId = resource.NormalizedPath;
		var reuseExistingSession = string.Equals(currentAudioResourceId, normalizedResourceId, StringComparison.OrdinalIgnoreCase);

		if (!reuseExistingSession)
		{
			StopPlaybackInternal(disposeSession: true, pauseOnly: false);
		}

		currentClipBegin = segment.Node.Audio?.ClipBegin ?? TimeSpan.Zero;
		currentClipEnd = ResolveClipBoundary(segmentIndex, segment);

		var sessionOpened = await EnsureAudioSessionAsync(normalizedResourceId, resource, reuseExistingSession).ConfigureAwait(false);
		if (!sessionOpened)
		{
			return;
		}

		await HandlePlaybackStartAsync(forceSeek).ConfigureAwait(false);

		if (!audioPlaybackService.HasSession)
		{
			return;
		}

		StartClipTimer(currentClipEnd);
		if (!isSeekPending)
		{
			await HighlightCurrentSegmentAsync(preferPreviousPage).ConfigureAwait(false);
		}
		await UpdateUiStateAsync().ConfigureAwait(false);

	}

	async Task HandleMissingAudioResourceAsync()
	{
		await viewModel.ShowInfoToastAsync("Audio clip missing for this passage.").ConfigureAwait(false);
		await NextAsync().ConfigureAwait(false);
	}

	async Task<bool> EnsureAudioSessionAsync(string normalizedResourceId, MediaOverlayAudioResource resource, bool reuseExistingSession)
	{
		return await dispatcher.DispatchAsync(async () =>
		{
			if (!audioPlaybackService.HasSession || !reuseExistingSession)
			{
				var opened = await audioPlaybackService.OpenResourceAsync(normalizedResourceId, resource.Content).ConfigureAwait(false);
				if (!opened)
				{
					await viewModel.ShowErrorToastAsync("Unable to start audio playback.").ConfigureAwait(false);
					currentAudioResourceId = null;
					return false;
				}
				currentAudioResourceId = normalizedResourceId;
			}
			return true;
		}).ConfigureAwait(false);
	}

	async Task HandlePlaybackStartAsync(bool forceSeek)
	{
		await dispatcher.DispatchAsync(() =>
		{
			var position = audioPlaybackService.CurrentPosition;
			if (double.IsNaN(position))
			{
				position = 0;
			}
			if (forceSeek)
			{
				var seekSeconds = pendingSeekOverrideSeconds ?? (currentClipBegin <= TimeSpan.Zero ? 0 : currentClipBegin.TotalSeconds);
				isSeekPending = true;
				audioPlaybackService.Seek(seekSeconds);
				pendingSeekOverrideSeconds = null;
				restoredPositionSeconds = null;
				if (!audioPlaybackService.IsPlaying)
				{
					audioPlaybackService.Play();
				}
			}
			else
			{
				isSeekPending = position < currentClipBegin.TotalSeconds;
				restoredPositionSeconds = null;
				if (!audioPlaybackService.IsPlaying)
				{
					audioPlaybackService.Play();
				}
			}
			isPlaying = true;
			return Task.CompletedTask;
		}).ConfigureAwait(false);
	}

	public async Task SeekAsync(double seconds)
	{
		if (!IsSupported || segments.Count == 0)
		{
			return;
		}

		// Clamp to available duration
		var total = GetActiveDurationSeconds();
		if (total is not null)
		{
			seconds = Math.Clamp(seconds, 0, total.Value);
		}

		// Find target segment by accumulating segment durations
		double cumulative = 0;
		for (int i = 0; i < segments.Count; i++)
		{
			var audio = segments[i].Node.Audio;
			if (audio?.ClipBegin is null || audio.ClipEnd is null)
			{
				// unknown length, treat as zero and pick this segment if we haven't found one
				if (i == segments.Count - 1)
				{
					break;
				}
				continue;
			}

			var segLen = (audio.ClipEnd.Value - (audio.ClipBegin ?? TimeSpan.Zero)).TotalSeconds;
			if (seconds < cumulative + segLen)
			{
				var offsetInSeg = Math.Max(0, seconds - cumulative);
				var seekSeconds = (audio.ClipBegin ?? TimeSpan.Zero).TotalSeconds + offsetInSeg;
				pendingSeekOverrideSeconds = seekSeconds;
				segmentIndex = i;
				await StartSegmentAsync(segments[segmentIndex], forceSeek: true, preferPreviousPage: seconds < cumulative).ConfigureAwait(false);
				return;
			}
			cumulative += segLen;
		}

		// Fallback: seek to last segment
		segmentIndex = Math.Clamp(segments.Count - 1, 0, segments.Count - 1);
		var lastSegment = segments[segmentIndex];
		if (lastSegment.Node.Audio is not null)
		{
			var clipBegin = lastSegment.Node.Audio.ClipBegin ?? TimeSpan.Zero;
			pendingSeekOverrideSeconds = clipBegin.TotalSeconds;
			await StartSegmentAsync(lastSegment, forceSeek: true).ConfigureAwait(false);
		}
	}

	double? GetCurrentPositionSeconds()
	{
		if (!audioPlaybackService.HasSession)
		{
			return null;
		}

		var pos = audioPlaybackService.CurrentPosition;
		if (double.IsNaN(pos) || double.IsInfinity(pos))
		{
			return null;
		}

		// Sum durations of segments before current index
		double cumulative = 0;
		for (int i = 0; i < segmentIndex; i++)
		{
			var audio = segments[i].Node.Audio;
			if (audio?.ClipBegin is not null && audio.ClipEnd is not null)
			{
				cumulative += (audio.ClipEnd.Value - (audio.ClipBegin ?? TimeSpan.Zero)).TotalSeconds;
			}
		}

		// Add current playback position relative to current clip begin
		var currentRelative = pos - (currentClipBegin.TotalSeconds);
		if (currentRelative < 0)
		{
			currentRelative = 0;
		}

		return cumulative + currentRelative;
	}


	bool TryGetAudioResource(string? source, out MediaOverlayAudioResource resource)
	{
		resource = null!;
		if (string.IsNullOrWhiteSpace(source))
		{
			return false;
		}

		var normalized = MediaOverlayPathHelper.Normalize(source);
		if (audioCache.TryGetValue(normalized, out var cached))
		{
			resource = cached;
			return true;
		}

		var fileName = MediaOverlayPathHelper.ExtractFileName(source);
		var match = audioCache.Values.FirstOrDefault(value =>
			string.Equals(MediaOverlayPathHelper.ExtractFileName(value.NormalizedPath), fileName, StringComparison.OrdinalIgnoreCase));
		if (match is not null)
		{
			resource = match;
			return true;
		}

		resource = null!;
		return false;
	}

	async Task HighlightCurrentSegmentAsync(bool preferPreviousPage = false)
	{
		if (!isWebViewReady || segments.Count == 0)
		{
			return;
		}

		segmentIndex = Math.Clamp(segmentIndex, 0, segments.Count - 1);
		var segment = segments[segmentIndex];
		var fragmentLiteral = JsonSerializer.Serialize(segment.FragmentId, serializerOptions);
		var activeLiteral = JsonSerializer.Serialize(ActiveClass, serializerOptions);
		var playbackLiteral = JsonSerializer.Serialize(PlaybackClass, serializerOptions);
		var navigationLiteral = JsonSerializer.Serialize(preferPreviousPage ? "prev" : "next", serializerOptions);

		await dispatcher.DispatchAsync(async () =>
		{
			await webView.EvaluateJavaScriptAsync($"ensureFragmentVisibleUsingNext({fragmentLiteral}, {navigationLiteral})");
			await webView.EvaluateJavaScriptAsync($"highlightMediaOverlayFragment({fragmentLiteral}, {activeLiteral}, {playbackLiteral})");
		}).ConfigureAwait(false);
	}

	Task<string> ClearHighlightAsync()
	{
		var activeLiteral = JsonSerializer.Serialize(ActiveClass, serializerOptions);
		var playbackLiteral = JsonSerializer.Serialize(PlaybackClass, serializerOptions);
		return dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"clearMediaOverlayHighlight({activeLiteral}, {playbackLiteral})"));
	}

	async Task UpdateUiStateAsync()
	{
		if (!IsSupported || !isWebViewReady)
		{
			return;
		}

		var payload = new
		{
			enabled = isEnabled,
			playing = isPlaying,
			segmentIndex = segments.Count == 0 ? 0 : segmentIndex + 1,
			segmentCount = segments.Count,
			chapterTitle = GetCurrentChapterTitle(),
			durationSeconds = GetActiveDurationSeconds(),
			positionSeconds = GetCurrentPositionSeconds() ?? restoredPositionSeconds
		};

		await InvokeScriptAsync("updateMediaOverlayPlaybackState", payload).ConfigureAwait(false);
		RaisePlaybackProgressChanged();
	}

	Task InvokeScriptAsync(string functionName, object? payload = null)
	{
		return dispatcher.DispatchAsync(async () =>
		{
			var script = payload is null
				? $"{functionName}();"
				: $"{functionName}({JsonSerializer.Serialize(payload, serializerOptions)});";
			try
			{
				await webView.EvaluateJavaScriptAsync(script).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.TraceError($"JavaScript invocation failed: {ex}");
			}
		});
	}

	void StopPlaybackInternal(bool disposeSession = true, bool pauseOnly = false)
	{
		// Ensure UI-affecting cleanup runs on the UI thread.
		if (dispatcher.IsDispatchRequired)
		{
			dispatcher.Dispatch(() => StopPlaybackInternal(disposeSession, pauseOnly));
			return;
		}

		if (clipTimer is not null)
		{
			clipTimer.Stop();
			clipTimer.Tick -= OnClipTimerTick;
			clipTimer = null;
		}

		if (audioPlaybackService is not null && audioPlaybackService.HasSession)
		{
			var lastPosition = GetCurrentPositionSeconds();
			if (disposeSession && lastPosition is double pos)
			{
				restoredPositionSeconds = pos;
			}

			if (pauseOnly)
			{
				audioPlaybackService.Pause();
			}
			else
			{
				audioPlaybackService.Stop();
			}

			if (disposeSession)
			{
				audioPlaybackService.Dispose();
			}
		}

		if (disposeSession)
		{
			currentAudioResourceId = null;
		}
		isPlaying = false;
		isSeekPending = false;
		currentClipEnd = null;
	}

	void StartClipTimer(TimeSpan? clipEnd)
	{
		if (dispatcher.IsDispatchRequired)
		{
			dispatcher.Dispatch(() => StartClipTimer(clipEnd));
			return;
		}

		// Update the clip boundary for the running timer.
		currentClipEnd = clipEnd;

		if (clipTimer is not null)
		{
			// Timer already running; just update the clip end boundary and continue.
			return;
		}

		clipTimer = dispatcher.CreateTimer();
		clipTimer.Interval = TimeSpan.FromMilliseconds(120);
		clipTimer.Tick += OnClipTimerTick;
		clipTimer.Start();
	}

	async Task<(int index, int count)> GetVisibleSegmentPositionAsync(string fragmentId, IReadOnlyList<string> allFragmentIds)
	{
		var fragmentLiteral = JsonSerializer.Serialize(fragmentId, serializerOptions);
		var listLiteral = JsonSerializer.Serialize(allFragmentIds, serializerOptions);
		var script = $"getVisibleSegmentPosition({fragmentLiteral}, {listLiteral})";
		try
		{
			var raw = await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync(script));

			raw = raw.Replace("\\", "");
			System.Diagnostics.Debug.WriteLine($"getVisibleSegmentPosition returned: {raw}");
			if (string.IsNullOrWhiteSpace(raw))
			{
				System.Diagnostics.Trace.TraceWarning("getVisibleSegmentPosition returned empty result.");
				return (-1, 0);
			}
			using var doc = JsonDocument.Parse(raw);
			var root = doc.RootElement;
			var index = root.TryGetProperty("index", out var jIndex) ? jIndex.GetInt32() : -1;
			var count = root.TryGetProperty("count", out var jCount) ? jCount.GetInt32() : 0;
			return (index, count);
		}

		catch (JsonException ex)
		{
			System.Diagnostics.Trace.TraceWarning($"getVisibleSegmentPosition JSON parse failed: {ex.Message}");
			return (-1, 0);
		}
	}

	async Task<int> FindFirstVisibleSegmentIndexAsync(IReadOnlyList<string> allFragmentIds)
	{
		for (int i = 0; i < segments.Count; i++)
		{
			var (visibleIndex, _) = await GetVisibleSegmentPositionAsync(segments[i].FragmentId, allFragmentIds).ConfigureAwait(false);
			if (visibleIndex >= 0)
			{
				return i;
			}
		}
		return -1;
	}

	async void OnClipTimerTick(object? sender, EventArgs e)
	{
		if (!audioPlaybackService.HasSession)
		{
			// No active player - stop the timer.
			clipTimer?.Stop();
			clipTimer = null;
			return;
		}

		var positionSeconds = audioPlaybackService.CurrentPosition;
		if (double.IsNaN(positionSeconds) || double.IsInfinity(positionSeconds))
		{
			return;
		}

		if (isSeekPending)
		{
			var beginSeconds = currentClipBegin.TotalSeconds;
			if (positionSeconds >= beginSeconds)
			{
				isSeekPending = false;
			}
			else
			{
				return;
			}
		}

		await HighlightCurrentSegmentAsync();

		// Throttle UI position updates to once per second to reduce JS churn
		var curSec = (int)Math.Floor(positionSeconds);
		if (!lastReportedSecond.HasValue || lastReportedSecond.Value != curSec)
		{
			lastReportedSecond = curSec;
			await UpdateUiStateAsync();
		}

		if (currentClipEnd is not null && positionSeconds >= currentClipEnd.Value.TotalSeconds)
		{
			// Do not stop the timer here; keep it running for the whole audio resource.
			await AdvanceAfterSegmentAsync();
		}
	}

	async void OnPlaybackEnded(object? sender, EventArgs e)
	{
		clipTimer?.Stop();
		clipTimer = null;
		await AdvanceAfterSegmentAsync();
	}

	Task AdvanceAfterSegmentAsync()
	{
		return dispatcher.DispatchAsync(async () =>
		{
			if (segmentIndex < segments.Count - 1)
			{
				var nextIndex = segmentIndex + 1;
				var nextSegment = segments[nextIndex];

				if (await IsCurrentSegmentLastOnPageAsync())
				{
					await HandleLastSegmentOnPageAsync(nextIndex, nextSegment);
					return;
				}

				await HandleNextSegmentAsync(nextIndex, nextSegment);
			}
			else
			{
				await FinishDocumentAsync();
			}
		});
	}

	async Task<bool> IsCurrentSegmentLastOnPageAsync()
	{
		var allFragmentIds = segments.Select(s => s.FragmentId).ToList();
		var (visibleIndex, visibleCount) = await GetVisibleSegmentPositionAsync(segments[segmentIndex].FragmentId, allFragmentIds);
		return visibleIndex >= 0 && visibleIndex == visibleCount - 1 && visibleCount > 0;
	}

	async Task HandleLastSegmentOnPageAsync(int nextIndex, MediaOverlaySegment nextSegment)
	{
		System.Diagnostics.Trace.TraceInformation($"Current segment {segmentIndex} is last on page; advancing page via JS next.");
		await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("handleNextCommand()"));

		var nextHasResourceAfter = TryGetAudioResource(nextSegment.Node.Audio?.Source, out var nextResourceAfter);
		var sameResourceAfter = nextHasResourceAfter && string.Equals(nextResourceAfter?.NormalizedPath, currentAudioResourceId, StringComparison.OrdinalIgnoreCase);
		segmentIndex = nextIndex;
		if (sameResourceAfter && audioPlaybackService.HasSession)
		{
			await ContinuePlaybackWithSameResourceAsync(nextSegment);
		}
		else
		{
			await StartSegmentAsync(segments[segmentIndex]);
		}
	}

	async Task HandleNextSegmentAsync(int nextIndex, MediaOverlaySegment nextSegment)
	{
		var nextHasResource = TryGetAudioResource(nextSegment.Node.Audio?.Source, out var nextResource);
		var sameResource = nextHasResource && string.Equals(nextResource?.NormalizedPath, currentAudioResourceId, StringComparison.OrdinalIgnoreCase);

		if (sameResource && audioPlaybackService.HasSession)
		{
			segmentIndex = nextIndex;
			await ContinuePlaybackWithSameResourceAsync(nextSegment);
		}
		else
		{
			segmentIndex = nextIndex;
			await StartSegmentAsync(segments[segmentIndex]);
		}
	}

	async Task ContinuePlaybackWithSameResourceAsync(MediaOverlaySegment segment)
	{
		currentClipBegin = segment.Node.Audio?.ClipBegin ?? TimeSpan.Zero;
		currentClipEnd = ResolveClipBoundary(segmentIndex, segment);
		var pos = audioPlaybackService.CurrentPosition;
		if (double.IsNaN(pos))
		{
			pos = 0;
		}
		isSeekPending = pos < currentClipBegin.TotalSeconds;
		if (!audioPlaybackService.IsPlaying)
		{
			audioPlaybackService.Play();
		}
		isPlaying = true;
		StartClipTimer(currentClipEnd);
		if (!isSeekPending)
		{
			await HighlightCurrentSegmentAsync();
			await UpdateUiStateAsync();
		}
	}

	TimeSpan? ResolveClipBoundary(int index, MediaOverlaySegment segment)
	{
		var audio = segment.Node.Audio;
		if (audio is null)
		{
			return null;
		}

		var clipBegin = audio.ClipBegin ?? TimeSpan.Zero;
		if (audio.ClipEnd is TimeSpan clipEnd && clipEnd > clipBegin)
		{
			return clipEnd;
		}

		var nextSegment = segments.ElementAtOrDefault(index + 1);
		if (nextSegment?.Node.Audio is MediaOverlayAudio nextAudio
			&& !string.IsNullOrWhiteSpace(audio.Source)
			&& !string.IsNullOrWhiteSpace(nextAudio.Source)
			&& MediaOverlayPathHelper.PathsReferToSameFile(nextAudio.Source, audio.Source)
			&& nextAudio.ClipBegin is TimeSpan nextClipBegin
			&& nextClipBegin > clipBegin)
		{
			return nextClipBegin;
		}

		return null;
	}

	async Task FinishDocumentAsync()
	{
		StopPlaybackInternal();
		await ClearHighlightAsync();
		await UpdateUiStateAsync();
		await viewModel.ShowInfoToastAsync("Reached the end of the narrated content.");
	}

	string GetCurrentChapterTitle()
	{
		var chapter = book.Chapters.ElementAtOrDefault(currentChapterIndex);
		return chapter?.Title ?? book.Title;
	}

	double? GetActiveDurationSeconds()
	{
		return currentChapterDurationSeconds ?? book.MediaOverlayDuration?.TotalSeconds;
	}

	static double? CalculateDurationSeconds(IReadOnlyList<MediaOverlaySegment> segmentList)
	{
		if (segmentList.Count == 0)
		{
			return null;
		}

		double totalSeconds = 0;
		var hasDuration = false;

		foreach (var segment in segmentList)
		{
			var audio = segment.Node.Audio;
			if (audio is null)
			{
				continue;
			}

			var clipBegin = audio.ClipBegin ?? TimeSpan.Zero;
			if (audio.ClipEnd is not TimeSpan clipEnd)
			{
				continue;
			}

			var clipLength = (clipEnd - clipBegin).TotalSeconds;
			if (clipLength <= 0)
			{
				continue;
			}

			totalSeconds += clipLength;
			hasDuration = true;
		}

		return hasDuration ? totalSeconds : null;
	}

	sealed record MediaOverlaySegment(MediaOverlayParallel Node, string FragmentId);
}