using System.Text.Encodings.Web;
using System.Text.Json;
using Plugin.Maui.Audio;

namespace EpubReader.MediaOverlay;

/// <summary>
/// Coordinates media overlay playback, highlighting, and UI updates for the reader surface.
/// </summary>
public partial class MediaOverlayPlaybackManager : IDisposable
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
	readonly Dictionary<string, MediaOverlayAudioResource> audioCache = [with(StringComparer.OrdinalIgnoreCase)];
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
	long? lastUiUpdateAtMs = null;
	double? pendingSeekOverrideSeconds;
	double? restoredPositionSeconds;
	MediaOverlayPlaybackProgress? pendingRestore;
	bool isApplyingRestore;
	bool pendingChapterAutoPlay;

	public event EventHandler<MediaOverlayPlaybackProgress>? PlaybackProgressChanged;
	public event EventHandler? ChapterAdvanceRequested;

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
		ArgumentNullException.ThrowIfNull(audioManager);
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
		foreach (MediaOverlayAudioResource? resource in book.MediaOverlayAudio.DistinctBy(res => res.NormalizedPath, StringComparer.OrdinalIgnoreCase))
		{
			audioCache[resource.NormalizedPath] = resource;
		}

		System.Diagnostics.Trace.TraceInformation($"Media overlay book context updated: overlays={book.MediaOverlays.Count}, audioResources={book.MediaOverlayAudio.Count}, cachedAudio={audioCache.Count}, currentChapter={book.CurrentChapter}");
	}

	public async Task InitializeUiAsync()
	{
		if (!IsSupported)
		{
			System.Diagnostics.Debug.WriteLine("Media overlay playback not supported for this book; skipping UI initialization.");
			await InvokeScriptAsync("setMediaOverlayVisibility", false);
			return;
		}

		List<Task> tasks =
		[
			InvokeScriptAsync("setMediaOverlayVisibility", true),
			InvokeScriptAsync("setMediaOverlayReaderModeHidden", isReaderModeHidden)
		];

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
		await Task.WhenAll(tasks);
	}

	public async Task SetReaderModeHiddenAsync(bool hidden)
	{
		isReaderModeHidden = hidden;

		if (!IsSupported || !isWebViewReady)
		{
			return;
		}

		await InvokeScriptAsync("setMediaOverlayReaderModeHidden", hidden);
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

		if (currentChapterIndex != chapterIndex)
		{
			await UpdateChapterContextAsync(chapterIndex).ConfigureAwait(false);
		}

		await InitializeUiAsync();

		await ApplyPendingRestoreAsync();

		if (isEnabled)
		{
			await HighlightCurrentSegmentAsync();
		}

		if (pendingChapterAutoPlay && isEnabled && segments.Count > 0)
		{
			pendingChapterAutoPlay = false;
			await PlayAsync();
			return;
		}

		await UpdateUiStateAsync();
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

		int chapterIndex = currentChapterIndex >= 0 ? currentChapterIndex : book.CurrentChapter;
		string? fragmentId = segments.Count > 0 && segmentIndex >= 0 && segmentIndex < segments.Count
			? segments[segmentIndex].FragmentId
			: null;
		double? position = GetCurrentPositionSeconds() ?? restoredPositionSeconds;
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

		MediaOverlayPlaybackProgress? restore = pendingRestore;
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
				await HighlightCurrentSegmentAsync();
			}
			else
			{
				await ClearHighlightAsync();
			}
			await UpdateUiStateAsync();
		}
		finally
		{
			isApplyingRestore = false;
		}
	}

	void ApplyRestoredAnchor(MediaOverlayPlaybackProgress restore)
	{
		int target = restore.SegmentIndex;
		if (!string.IsNullOrWhiteSpace(restore.FragmentId))
		{
			int idx = segments.FindIndex(seg => string.Equals(seg.FragmentId, restore.FragmentId, StringComparison.Ordinal));
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
		double? total = GetActiveDurationSeconds();
		if (total is not null)
		{
			positionSeconds = Math.Clamp(positionSeconds, 0, total.Value);
		}

		bool hasAnyDuration = false;
		bool mapped = false;
		double cumulative = 0;
		for (int i = 0; i < segments.Count; i++)
		{
			MediaOverlayAudio? audio = segments[i].Node.Audio;
			if (audio?.ClipBegin is null || audio.ClipEnd is null)
			{
				continue;
			}

			hasAnyDuration = true;

			double segLen = (audio.ClipEnd.Value - (audio.ClipBegin ?? TimeSpan.Zero)).TotalSeconds;
			if (positionSeconds < cumulative + segLen)
			{
				segmentIndex = i;
				double offsetInSeg = Math.Max(0, positionSeconds - cumulative);
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

		MediaOverlayPlaybackProgress? snapshot = GetPlaybackProgressSnapshot();
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
			await HighlightCurrentSegmentAsync();
		}

		await UpdateUiStateAsync();
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
			await SetEnabledAsync(true);
			return;
		}

		if (segments.Count == 0)
		{
			await viewModel.ShowInfoToastAsync("Audio isn't available for this section.").ConfigureAwait(false);
			return;
		}


		// If the saved/restored segment isn't visible on the current page, prefer
		// the first visible segment on the page (and make that the new saved segment).
		List<string> allFragmentIds = [.. segments.Select(s => s.FragmentId)];
		// If the current segmentIndex points at a valid segment, check visibility.
		if (segmentIndex >= 0 && segmentIndex < segments.Count)
		{
			(int visibleIndex, int _) = await GetVisibleSegmentPositionAsync(segments[segmentIndex].FragmentId, allFragmentIds);
			if (visibleIndex < 0)
			{
				int firstVisible = await FindFirstVisibleSegmentIndexAsync(allFragmentIds);
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
			int firstVisible = await FindFirstVisibleSegmentIndexAsync(allFragmentIds);
			if (firstVisible >= 0)
			{
				segmentIndex = firstVisible;
				pendingSeekOverrideSeconds = segments[segmentIndex].Node.Audio?.ClipBegin?.TotalSeconds ?? 0;
			}
		}

		segmentIndex = Math.Clamp(segmentIndex, 0, segments.Count - 1);
		// If a restore (or seek) provided an explicit position, ensure playback begins at it.
		bool forceSeek = pendingSeekOverrideSeconds is not null;
		await StartSegmentAsync(segments[segmentIndex], forceSeek: forceSeek);
	}

	public async Task PauseAsync()
	{
		if (!isPlaying)
		{
			return;
		}

		StopPlaybackInternal(disposeSession: false, pauseOnly: true);
		await UpdateUiStateAsync();
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
				await StartSegmentAsync(segments[segmentIndex], forceSeek: true);
			}
			else
			{
				await HighlightCurrentSegmentAsync();
				await UpdateUiStateAsync();
			}
		}
		else
		{
			await FinishDocumentAsync();
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
				await StartSegmentAsync(segments[segmentIndex], forceSeek: true, preferPreviousPage: true);
			}
			else
			{
				await HighlightCurrentSegmentAsync(preferPreviousPage: true);
				await UpdateUiStateAsync();
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
		await ClearHighlightAsync();
		await UpdateUiStateAsync();
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposed)
		{
			return;
		}
		if (disposing)
		{
			StopPlaybackInternal();
			clipTimer = null;
			audioPlaybackService.Dispose();
		}
		disposed = true;
	}
	async Task UpdateChapterContextAsync(int chapterIndex)
	{
		currentChapterIndex = chapterIndex;
		Chapter? chapter = book.Chapters.ElementAtOrDefault(chapterIndex);
		if (chapter is null)
		{
			segments = [];
			currentChapterDurationSeconds = null;
			return;
		}

		List<MediaOverlayDocument> documents = ResolveDocumentsForChapter(chapter.FileName);
		segments = BuildSegments(documents, chapter.FileName);
		currentChapterDurationSeconds = CalculateDurationSeconds(segments);
		segmentIndex = 0;

		string matchedDocumentSummary = documents.Count == 0
			? "none"
			: string.Join(", ", documents.Select(document => $"{document.Id}:{document.FlattenedNodes.Count} nodes/{document.AssociatedContentDocuments.Count} refs"));
		System.Diagnostics.Trace.TraceInformation($"Media overlay chapter context: chapterIndex={chapterIndex}, chapterFile='{chapter.FileName}', matchedDocuments={documents.Count}, segments={segments.Count}, cachedAudio={audioCache.Count}, durationSeconds={currentChapterDurationSeconds?.ToString() ?? "null"}, docs=[{matchedDocumentSummary}]");

		if (isEnabled && segments.Count == 0)
		{
			await viewModel.ShowInfoToastAsync("Audio isn't available for this section.").ConfigureAwait(false);
		}
	}

	List<MediaOverlayDocument> ResolveDocumentsForChapter(string chapterFileName)
	{
		string normalizedChapterPath = MediaOverlayPathHelper.Normalize(chapterFileName);
		string chapterFileNameOnly = MediaOverlayPathHelper.ExtractFileName(chapterFileName);
		List<MediaOverlayDocument> documents = [.. book.MediaOverlays.Where(document =>
			document.AssociatedContentDocuments.Any(content =>
				MediaOverlayPathHelper.PathsReferToSameFile(content, normalizedChapterPath) ||
				string.Equals(MediaOverlayPathHelper.ExtractFileName(content), chapterFileNameOnly, StringComparison.OrdinalIgnoreCase)) ||
			document.FlattenedNodes.Any(node => NodeTargetsChapter(node, normalizedChapterPath, chapterFileNameOnly)))];

		System.Diagnostics.Trace.TraceInformation($"ResolveDocumentsForChapter: chapter='{chapterFileName}', normalized='{normalizedChapterPath}', fileName='{chapterFileNameOnly}', matched={documents.Count}");
		return documents;
	}

	static List<MediaOverlaySegment> BuildSegments(IEnumerable<MediaOverlayDocument> documents, string chapterFileName)
	{
		ArgumentNullException.ThrowIfNull(documents);
		List<MediaOverlayDocument> documentList = [.. documents];

		string normalizedChapterPath = MediaOverlayPathHelper.Normalize(chapterFileName);
		string chapterFileNameOnly = MediaOverlayPathHelper.ExtractFileName(chapterFileName);
		if (string.IsNullOrWhiteSpace(chapterFileNameOnly))
		{
			return [];
		}

		// Matches the chapterId used by EbookService when building combined.html sections,
		// and by HtmlAgilityPackExtensions.BuildCombinedFragmentId when rewriting element IDs.
		string chapterId = Path.GetFileNameWithoutExtension(chapterFileName);

		List<MediaOverlaySegment> list = [];
		HashSet<string> seen = [with(StringComparer.OrdinalIgnoreCase)];
		int candidateNodes = 0;

		foreach (MediaOverlayDocument? document in documentList)
		{
			foreach (MediaOverlayParallel node in document.FlattenedNodes)
			{
				if (node.Text is null || node.Audio is null)
				{
					continue;
				}

				(string? sourcePath, string? fragment) = MediaOverlayPathHelper.SplitSource(node.Text.Source);
				if (string.IsNullOrWhiteSpace(fragment) || !PathsTargetChapter(sourcePath, normalizedChapterPath, chapterFileNameOnly))
				{
					continue;
				}

				candidateNodes++;

				string key = $"{MediaOverlayPathHelper.Normalize(sourcePath)}#{fragment}|{MediaOverlayPathHelper.Normalize(node.Audio.Source)}|{node.Audio.ClipBegin?.Ticks ?? 0}|{node.Audio.ClipEnd?.Ticks ?? 0}";
				if (!seen.Add(key))
				{
					continue;
				}

				// Prefix the fragment with the chapter ID to match the rewritten DOM IDs in combined.html
				// (e.g., "someId" -> "p002__someId", mirroring BuildCombinedFragmentId).
				string combinedFragmentId = $"{chapterId}__{Uri.UnescapeDataString(fragment.Trim())}";
				list.Add(new MediaOverlaySegment(node, combinedFragmentId));
			}
		}

		System.Diagnostics.Trace.TraceInformation($"BuildSegments: chapter='{chapterFileName}', documents={documentList.Count}, candidates={candidateNodes}, uniqueSegments={list.Count}");

		return list;
	}

	static bool NodeTargetsChapter(MediaOverlayParallel node, string normalizedChapterPath, string chapterFileNameOnly)
	{
		if (node.Text is null)
		{
			return false;
		}

		(string? sourcePath, string? _) = MediaOverlayPathHelper.SplitSource(node.Text.Source);
		return PathsTargetChapter(sourcePath, normalizedChapterPath, chapterFileNameOnly);
	}

	static bool PathsTargetChapter(string? sourcePath, string normalizedChapterPath, string chapterFileNameOnly)
	{
		if (string.IsNullOrWhiteSpace(sourcePath))
		{
			return false;
		}

		return MediaOverlayPathHelper.PathsReferToSameFile(sourcePath, normalizedChapterPath) ||
			string.Equals(MediaOverlayPathHelper.ExtractFileName(sourcePath), chapterFileNameOnly, StringComparison.OrdinalIgnoreCase);
	}

	async Task StartSegmentAsync(MediaOverlaySegment segment, bool forceSeek = false, bool preferPreviousPage = false)
	{
		if (!TryGetAudioResource(segment.Node.Audio?.Source, out MediaOverlayAudioResource? resource))
		{
			System.Diagnostics.Trace.TraceWarning($"StartSegmentAsync could not resolve audio resource for source '{segment.Node.Audio?.Source}'.");
			await HandleMissingAudioResourceAsync();
			return;
		}

		string normalizedResourceId = resource.NormalizedPath;
		bool reuseExistingSession = string.Equals(currentAudioResourceId, normalizedResourceId, StringComparison.OrdinalIgnoreCase);

		if (!reuseExistingSession)
		{
			StopPlaybackInternal(disposeSession: true, pauseOnly: false);
		}

		currentClipBegin = segment.Node.Audio?.ClipBegin ?? TimeSpan.Zero;
		currentClipEnd = ResolveClipBoundary(segmentIndex, segment);

		bool sessionOpened = await EnsureAudioSessionAsync(normalizedResourceId, resource, reuseExistingSession);
		if (!sessionOpened)
		{
			return;
		}

		await HandlePlaybackStartAsync(forceSeek);

		if (!audioPlaybackService.HasSession)
		{
			return;
		}

		StartClipTimer(currentClipEnd);
		// If this start was triggered by an explicit seek, update the highlight immediately
		// so the UI reflects the user's scrub action instead of waiting for the timer.
		// or the UI is not yet in sync with playback.
		if (forceSeek || !isSeekPending)
		{
			await HighlightCurrentSegmentAsync(preferPreviousPage);
		}

		await UpdateUiStateAsync();

	}

	async Task HandleMissingAudioResourceAsync()
	{
		await viewModel.ShowInfoToastAsync("Audio clip missing for this passage.");
		await NextAsync();
	}

	async Task<bool> EnsureAudioSessionAsync(string normalizedResourceId, MediaOverlayAudioResource resource, bool reuseExistingSession)
	{
		return await dispatcher.DispatchAsync(async () =>
		{
			if (!audioPlaybackService.HasSession || !reuseExistingSession)
			{
				System.Diagnostics.Trace.TraceInformation($"Opening media overlay audio session: resource='{normalizedResourceId}', bytes={resource.Content.Length}, reuseExistingSession={reuseExistingSession}, contentType='{resource.ContentType ?? "unknown"}'");
				bool opened = await audioPlaybackService.OpenResourceAsync(normalizedResourceId, resource.Content);
				if (!opened)
				{
					System.Diagnostics.Trace.TraceWarning($"Audio session failed to open for resource '{normalizedResourceId}'.");
					await viewModel.ShowErrorToastAsync("Unable to start audio playback.");
					currentAudioResourceId = null;
					return false;
				}
				currentAudioResourceId = normalizedResourceId;
			}
			return true;
		});
	}

	async Task HandlePlaybackStartAsync(bool forceSeek)
	{
		await dispatcher.DispatchAsync(() =>
		{
			double position = audioPlaybackService.CurrentPosition;
			if (double.IsNaN(position))
			{
				position = 0;
			}
			if (forceSeek)
			{
				double seekSeconds = pendingSeekOverrideSeconds ?? (currentClipBegin <= TimeSpan.Zero ? 0 : currentClipBegin.TotalSeconds);
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
		});
	}

	public async Task SeekAsync(double seconds)
	{
		if (!IsSupported || segments.Count == 0)
		{
			return;
		}

		// Clamp to available duration
		double? total = GetActiveDurationSeconds();
		if (total is not null)
		{
			seconds = Math.Clamp(seconds, 0, total.Value);
		}

		// Find target segment by accumulating segment durations
		double cumulative = 0;
		for (int i = 0; i < segments.Count; i++)
		{
			MediaOverlayAudio? audio = segments[i].Node.Audio;
			if (audio?.ClipBegin is null || audio.ClipEnd is null)
			{
				// unknown length, treat as zero and pick this segment if we haven't found one
				if (i == segments.Count - 1)
				{
					break;
				}
				continue;
			}

			double segLen = (audio.ClipEnd.Value - (audio.ClipBegin ?? TimeSpan.Zero)).TotalSeconds;
			if (seconds < cumulative + segLen)
			{
				double offsetInSeg = Math.Max(0, seconds - cumulative);
				double seekSeconds = (audio.ClipBegin ?? TimeSpan.Zero).TotalSeconds + offsetInSeg;
				pendingSeekOverrideSeconds = seekSeconds;
				segmentIndex = i;
				await StartSegmentAsync(segments[segmentIndex], forceSeek: true, preferPreviousPage: seconds < cumulative);
				return;
			}
			cumulative += segLen;
		}

		// Fallback: seek to last segment
		segmentIndex = Math.Clamp(segments.Count - 1, 0, segments.Count - 1);
		MediaOverlaySegment lastSegment = segments[segmentIndex];
		if (lastSegment.Node.Audio is not null)
		{
			TimeSpan clipBegin = lastSegment.Node.Audio.ClipBegin ?? TimeSpan.Zero;
			pendingSeekOverrideSeconds = clipBegin.TotalSeconds;
			await StartSegmentAsync(lastSegment, forceSeek: true);
		}
	}

	double? GetCurrentPositionSeconds()
	{
		if (!audioPlaybackService.HasSession)
		{
			return null;
		}

		double pos = audioPlaybackService.CurrentPosition;
		if (double.IsNaN(pos) || double.IsInfinity(pos))
		{
			return null;
		}

		// Sum durations of segments before current index
		double cumulative = 0;
		for (int i = 0; i < segmentIndex; i++)
		{
			MediaOverlayAudio? audio = segments[i].Node.Audio;
			if (audio?.ClipBegin is not null && audio.ClipEnd is not null)
			{
				cumulative += (audio.ClipEnd.Value - (audio.ClipBegin ?? TimeSpan.Zero)).TotalSeconds;
			}
		}

		// Add current playback position relative to current clip begin
		double currentRelative = pos - (currentClipBegin.TotalSeconds);
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
			System.Diagnostics.Trace.TraceWarning("TryGetAudioResource called with an empty source.");
			return false;
		}

		string normalized = MediaOverlayPathHelper.Normalize(source);
		if (audioCache.TryGetValue(normalized, out MediaOverlayAudioResource? cached))
		{
			resource = cached;
			return true;
		}

		MediaOverlayAudioResource? relativeMatch = audioCache.Values.FirstOrDefault(value =>
			MediaOverlayPathHelper.PathsReferToSameFile(value.RelativePath, normalized) ||
			MediaOverlayPathHelper.PathsReferToSameFile(value.NormalizedPath, normalized));
		if (relativeMatch is not null)
		{
			resource = relativeMatch;
			return true;
		}

		string fileName = MediaOverlayPathHelper.ExtractFileName(source);
		MediaOverlayAudioResource? match = audioCache.Values.FirstOrDefault(value =>
			string.Equals(MediaOverlayPathHelper.ExtractFileName(value.NormalizedPath), fileName, StringComparison.OrdinalIgnoreCase));
		if (match is not null)
		{
			resource = match;
			return true;
		}

		string[] sampleKeys = [.. audioCache.Keys.Take(5)];
		System.Diagnostics.Trace.TraceWarning($"TryGetAudioResource failed: source='{source}', normalized='{normalized}', fileName='{fileName}', cachedAudio={audioCache.Count}, sampleKeys=[{string.Join(", ", sampleKeys)}]");

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
		MediaOverlaySegment segment = segments[segmentIndex];
		string fragmentLiteral = JsonSerializer.Serialize(segment.FragmentId, serializerOptions);
		string activeLiteral = JsonSerializer.Serialize(ActiveClass, serializerOptions);
		string playbackLiteral = JsonSerializer.Serialize(PlaybackClass, serializerOptions);
		string navigationLiteral = JsonSerializer.Serialize(preferPreviousPage ? "prev" : "next", serializerOptions);

		await dispatcher.DispatchAsync(async () =>
		{
			await webView.EvaluateJavaScriptAsync($"ensureFragmentVisibleUsingNext({fragmentLiteral}, {navigationLiteral})");
			await webView.EvaluateJavaScriptAsync($"highlightMediaOverlayFragment({fragmentLiteral}, {activeLiteral}, {playbackLiteral})");
		});
	}

	Task<string> ClearHighlightAsync()
	{
		string activeLiteral = JsonSerializer.Serialize(ActiveClass, serializerOptions);
		string playbackLiteral = JsonSerializer.Serialize(PlaybackClass, serializerOptions);
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

		await InvokeScriptAsync("updateMediaOverlayPlaybackState", payload);
		RaisePlaybackProgressChanged();
	}

	Task InvokeScriptAsync(string functionName, object? payload = null)
	{
		return dispatcher.DispatchAsync(async () =>
		{
			string script = payload is null
				? $"{functionName}();"
				: $"{functionName}({JsonSerializer.Serialize(payload, serializerOptions)});";
			try
			{
				await webView.EvaluateJavaScriptAsync(script);
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
			double? lastPosition = GetCurrentPositionSeconds();
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
		clipTimer.Interval = TimeSpan.FromMilliseconds(50);
		clipTimer.Tick += OnClipTimerTick;
		clipTimer.Start();
	}

	async Task<(int index, int count)> GetVisibleSegmentPositionAsync(string fragmentId, IReadOnlyList<string> allFragmentIds)
	{
		string fragmentLiteral = JsonSerializer.Serialize(fragmentId, serializerOptions);
		string listLiteral = JsonSerializer.Serialize(allFragmentIds, serializerOptions);
		string script = $"getVisibleSegmentPosition({fragmentLiteral}, {listLiteral})";
		try
		{
			string raw = await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync(script));

			raw = raw.Replace("\\", "");
			System.Diagnostics.Debug.WriteLine($"getVisibleSegmentPosition returned: {raw}");
			if (string.IsNullOrWhiteSpace(raw))
			{
				System.Diagnostics.Trace.TraceWarning("getVisibleSegmentPosition returned empty result.");
				return (-1, 0);
			}
			using JsonDocument doc = JsonDocument.Parse(raw);
			JsonElement root = doc.RootElement;
			int index = root.TryGetProperty("index", out JsonElement jIndex) ? jIndex.GetInt32() : -1;
			int count = root.TryGetProperty("count", out JsonElement jCount) ? jCount.GetInt32() : 0;
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
			(int visibleIndex, int _) = await GetVisibleSegmentPositionAsync(segments[i].FragmentId, allFragmentIds);
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

		double positionSeconds = audioPlaybackService.CurrentPosition;
		if (double.IsNaN(positionSeconds) || double.IsInfinity(positionSeconds))
		{
			return;
		}

		if (isSeekPending)
		{
			double beginSeconds = currentClipBegin.TotalSeconds;
			if (positionSeconds >= beginSeconds)
			{
				isSeekPending = false;
			}
			else
			{
				return;
			}
		}

		// Always update highlight promptly for responsiveness
		await HighlightCurrentSegmentAsync();

		// Throttle UI payloads to ~200ms to reduce JS churn
		long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		if (!lastUiUpdateAtMs.HasValue || nowMs - lastUiUpdateAtMs.Value >= 200)
		{
			lastUiUpdateAtMs = nowMs;
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
				int nextIndex = segmentIndex + 1;
				MediaOverlaySegment nextSegment = segments[nextIndex];

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
		List<string> allFragmentIds = [.. segments.Select(s => s.FragmentId)];
		(int visibleIndex, int visibleCount) = await GetVisibleSegmentPositionAsync(segments[segmentIndex].FragmentId, allFragmentIds);
		return visibleIndex >= 0 && visibleIndex == visibleCount - 1 && visibleCount > 0;
	}

	async Task HandleLastSegmentOnPageAsync(int nextIndex, MediaOverlaySegment nextSegment)
	{
		System.Diagnostics.Trace.TraceInformation($"Current segment {segmentIndex} is last on page; advancing page via JS next.");
		await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("handleNextCommand()"));

		bool nextHasResourceAfter = TryGetAudioResource(nextSegment.Node.Audio?.Source, out MediaOverlayAudioResource? nextResourceAfter);
		bool sameResourceAfter = nextHasResourceAfter && string.Equals(nextResourceAfter?.NormalizedPath, currentAudioResourceId, StringComparison.OrdinalIgnoreCase);
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
		bool nextHasResource = TryGetAudioResource(nextSegment.Node.Audio?.Source, out MediaOverlayAudioResource? nextResource);
		bool sameResource = nextHasResource && string.Equals(nextResource?.NormalizedPath, currentAudioResourceId, StringComparison.OrdinalIgnoreCase);

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
		double pos = audioPlaybackService.CurrentPosition;
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
		MediaOverlayAudio? audio = segment.Node.Audio;
		if (audio is null)
		{
			return null;
		}

		TimeSpan clipBegin = audio.ClipBegin ?? TimeSpan.Zero;
		if (audio.ClipEnd is TimeSpan clipEnd && clipEnd > clipBegin)
		{
			return clipEnd;
		}

		MediaOverlaySegment? nextSegment = segments.ElementAtOrDefault(index + 1);
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
		if (isEnabled && NextChapterHasMedia())
		{
			StopPlaybackInternal();
			await ClearHighlightAsync();
			pendingChapterAutoPlay = true;
			ChapterAdvanceRequested?.Invoke(this, EventArgs.Empty);
			return;
		}

		StopPlaybackInternal();
		await ClearHighlightAsync();
		await UpdateUiStateAsync();
		await viewModel.ShowInfoToastAsync("Reached the end of the narrated content.");
	}

	bool NextChapterHasMedia()
	{
		int nextIndex = currentChapterIndex + 1;
		if (nextIndex >= book.Chapters.Count)
		{
			return false;
		}
		Chapter nextChapter = book.Chapters[nextIndex];
		List<MediaOverlayDocument> docs = ResolveDocumentsForChapter(nextChapter.FileName);
		return BuildSegments(docs, nextChapter.FileName).Count > 0;
	}

	string GetCurrentChapterTitle()
	{
		Chapter? chapter = book.Chapters.ElementAtOrDefault(currentChapterIndex);
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
		bool hasDuration = false;

		foreach (MediaOverlaySegment segment in segmentList)
		{
			MediaOverlayAudio? audio = segment.Node.Audio;
			if (audio is null)
			{
				continue;
			}

			TimeSpan clipBegin = audio.ClipBegin ?? TimeSpan.Zero;
			if (audio.ClipEnd is not TimeSpan clipEnd)
			{
				continue;
			}

			double clipLength = (clipEnd - clipBegin).TotalSeconds;
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