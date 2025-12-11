using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using EpubReader.Models;
using EpubReader.Models.MediaOverlays;
using EpubReader.ViewModels;
using Microsoft.Maui.Dispatching;
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
	IReadOnlyList<MediaOverlaySegment> segments = [];
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

		if (isEnabled)
		{
			await HighlightCurrentSegmentAsync().ConfigureAwait(false);
		}

		await UpdateUiStateAsync().ConfigureAwait(false);
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

        
		segmentIndex = Math.Clamp(segmentIndex, 0, segments.Count - 1);
		await StartSegmentAsync(segments[segmentIndex]).ConfigureAwait(false);
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

	async Task StartSegmentAsync(MediaOverlaySegment segment, bool forceSeek = false)
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
			await HighlightCurrentSegmentAsync().ConfigureAwait(false);
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
				var seekSeconds = currentClipBegin <= TimeSpan.Zero ? 0 : currentClipBegin.TotalSeconds;
				isSeekPending = true;
				audioPlaybackService.Seek(seekSeconds);
				if (!audioPlaybackService.IsPlaying)
				{
					audioPlaybackService.Play();
				}
			}
			else
			{
				isSeekPending = position < currentClipBegin.TotalSeconds;
				if (!audioPlaybackService.IsPlaying)
				{
					audioPlaybackService.Play();
				}
			}
			isPlaying = true;
			return Task.CompletedTask;
		}).ConfigureAwait(false);
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

	async Task HighlightCurrentSegmentAsync()
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

		await dispatcher.DispatchAsync( async() => {
		await webView.EvaluateJavaScriptAsync($"ensureFragmentVisibleUsingNext({fragmentLiteral})");
		await webView.EvaluateJavaScriptAsync($"highlightMediaOverlayFragment({fragmentLiteral}, {activeLiteral}, {playbackLiteral})");
		}).ConfigureAwait(false);
	}

	Task<string> ClearHighlightAsync()
	{
		var activeLiteral = JsonSerializer.Serialize(ActiveClass, serializerOptions);
		var playbackLiteral = JsonSerializer.Serialize(PlaybackClass, serializerOptions);
		return webView.EvaluateJavaScriptAsync($"clearMediaOverlayHighlight({activeLiteral}, {playbackLiteral})");
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
			durationSeconds = GetActiveDurationSeconds()
		};

		await InvokeScriptAsync("updateMediaOverlayPlaybackState", payload).ConfigureAwait(false);
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
			var raw = await webView.EvaluateJavaScriptAsync(script);
			
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

		// If we just cleared the pending state, apply highlighting now.
		if (!isSeekPending)
		{
			try
			{
				await HighlightCurrentSegmentAsync();
				await UpdateUiStateAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.TraceWarning($"Highlight apply failed: {ex.Message}");
			}
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
		await webView.EvaluateJavaScriptAsync("handleNextCommand()");

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