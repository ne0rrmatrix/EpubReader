using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Plugin.Maui.Audio;

namespace EpubReader.Views;

/// <summary>
/// Represents a page in a book application, providing functionality for displaying and interacting with book content.
/// </summary>
/// <remarks>The <see cref="BookPage"/> class is responsible for managing the user interface and interactions for
/// a single page of a book. It handles navigation, animations, and JavaScript interactions within a web view. The page
/// is initialized with a view model and a database interface, which are used for data binding and data operations,
/// respectively.</remarks>
public partial class BookPage : ContentPage, IDisposable
{
	bool disposedValue = false;
	bool isMenuOpen = false;
	const string externalLinkPrefix = "https://runcsharp.jump?";
	const uint animationDuration = 200u;
	BookViewModel ViewModel => (BookViewModel)BindingContext;
	Book book => ViewModel.Book;
	readonly IDb db;
	readonly ISyncService syncService;
	readonly WebViewHelper webViewHelper;
	readonly IAudioManager audioManager;
	MediaOverlayPlaybackManager? mediaOverlayManager;
	MediaOverlayPlaybackProgress? latestMediaOverlayProgress;
	DateTimeOffset lastMediaOverlayProgressSyncedAt = DateTimeOffset.MinValue;
	MediaOverlayPlaybackProgress? lastMediaOverlayProgressSent;
	bool loadSequenceStarted = false;
   bool navigateToChapterEndOnLoad = false;
	ToolbarItem? syncToolbarItem;

	// Slider-related state
	readonly List<int> chapterOffsets = [];
	int sliderTotalPages = 0;
	bool isSliderActive = false;
	CancellationTokenSource? settingsRefreshCancellationTokenSource;

	/// <summary>
	/// Initializes a new instance of the <see cref="BookPage"/> class with the specified view model and database.
	/// </summary>
	/// <remarks>This constructor sets up the page's data binding context and initializes necessary components. It
	/// also registers message handlers for JavaScript and settings messages using a weak reference messenger.</remarks>
	/// <param name="viewModel">The view model that provides data binding for the page.</param>
	/// <param name="db">The database interface used for data operations within the page.</param>
	/// <param name="syncService">The sync service for managing reading progress synchronization.</param>
	/// <param name="audioManager">The cross-platform audio manager used for narrated overlays.</param>
	public BookPage(BookViewModel viewModel, IDb db, ISyncService syncService, IAudioManager audioManager)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		this.syncService = syncService;
		this.audioManager = audioManager;
		webViewHelper = new(webView, db, syncService);
		ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		NativeLoadingOverlay.IsVisible = true;

		Dispatcher.Dispatch(async () => await UpdateSyncToolbarAsync());

		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, async (r, m) => await HandleJavascriptAsync(m.Value));
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => await HandleSettingsChangedAsync(m));
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await UpdateSyncToolbarAsync();
		if (!loadSequenceStarted)
		{
			await EnsureMediaOverlayManagerInitialized();
			await UpdateReaderModeOverlayAsync(ViewModel.IsReaderModeEnabled);
			loadSequenceStarted = true;
			webView.Navigated -= webView_Navigated;
			webView.Navigating -= webView_Navigating;
			webView.Navigated += webView_Navigated;
			webView.Navigating += webView_Navigating;
			await StartLoadSequenceAsync();
		}
	}

	async Task UpdateSyncToolbarAsync()
	{
		RemoveSyncToolbarItem();
		var currentShell = Shell.Current;
		if (currentShell is null)
		{
			return;
		}

		var isLocalAuth = await AuthenticationService.IsLocalOnlyModeAsync();
		if (syncService.IsLocalOnly || isLocalAuth)
		{
			return;
		}

		syncToolbarItem = new ToolbarItem
		{
			Text = "Sync",
			Priority = 0,
			Order = ToolbarItemOrder.Primary,
			Command = new Command(() => Dispatcher.Dispatch(async () => await SyncProgressNowAsync(ViewModel.CancellationTokenSource.Token)))
		};
		currentShell.ToolbarItems.Add(syncToolbarItem);
	}

	void RemoveSyncToolbarItem()
	{
		var currentShell = Shell.Current;
		if (currentShell is null)
		{
			syncToolbarItem = null;
			return;
		}

		if (syncToolbarItem is not null && currentShell.ToolbarItems.Contains(syncToolbarItem))
		{
			currentShell.ToolbarItems.Remove(syncToolbarItem);
		}
		syncToolbarItem = null;
	}

	/// <summary>
	/// Handles the actions to be performed when the page is disappearing.
	/// </summary>
	/// <remarks>This method unregisters all messages for the current instance and clears the toolbar items if the
	/// popup is not active. It also ensures the navigation bar is visible. Overrides the base <see cref="OnDisappearing"/>
	/// method.</remarks>
	protected override async void OnDisappearing()
	{
		var viewModel = BindingContext as BookViewModel;
		var isPopupActive = viewModel?.isPopupActive == true;
		if (!isPopupActive)
		{
			// Persist reading position before the page is torn down so reopening restores correctly.
			try
			{
				await SaveProgressAsync(CancellationToken.None);
			}
			catch (Exception ex)
			{
				Trace.TraceWarning($"Failed saving progress on page disappear: {ex.Message}");
			}

			WeakReferenceMessenger.Default.UnregisterAll(this);
			var currentShell = Shell.Current;
			if (currentShell?.ToolbarItems is not null && currentShell.ToolbarItems.Count > 0)
			{
				currentShell.ToolbarItems.Clear();
			}

			if (Application.Current?.Windows is { Count: > 0 } windows && windows[0].Page is Page currentPage)
			{
				Shell.SetNavBarIsVisible(currentPage, true);
			}
			// Reset load sequence when the page is truly disappearing (not just a popup)
			loadSequenceStarted = false;
			CancelPendingSettingsRefresh();
			// Allow a fresh combined.html load next time the book is opened.
			webViewHelper.ResetCombinedState();
		}

		// detach webview handlers we attached on appearing
		if (webView is not null)
		{
			webView.Navigated -= webView_Navigated;
			webView.Navigating -= webView_Navigating;
		}
		base.OnDisappearing();
	}

	async Task HandleSettingsChangedAsync(SettingsMessage message)
	{
		await webViewHelper.OnSettingsClickedAsync();
		await UpdateUiAppearance();

		if (!message.RequiresPaginationRefresh)
		{
			return;
		}

		SchedulePaginationRefresh();
	}

	void SchedulePaginationRefresh()
	{
		CancelPendingSettingsRefresh();
		settingsRefreshCancellationTokenSource = new CancellationTokenSource();
		var token = settingsRefreshCancellationTokenSource.Token;
		_ = DebouncedRefreshPaginationAsync(token);
	}

	void CancelPendingSettingsRefresh()
	{
		if (settingsRefreshCancellationTokenSource is null)
		{
			return;
		}

		settingsRefreshCancellationTokenSource.Cancel();
		settingsRefreshCancellationTokenSource.Dispose();
		settingsRefreshCancellationTokenSource = null;
	}

	async Task DebouncedRefreshPaginationAsync(CancellationToken token)
	{
		try
		{
			await Task.Delay(TimeSpan.FromMilliseconds(400), token);
			token.ThrowIfCancellationRequested();
			await RefreshPaginationStateAsync(ViewModel.CancellationTokenSource.Token);
		}
		catch (OperationCanceledException)
		{
			Trace.TraceInformation("Settings pagination refresh canceled.");
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Debounced settings pagination refresh failed: {ex.Message}");
		}
	}


	/// <summary>
	/// Handles the tap event on the grid area, triggering animations for translation, scaling, and fading.
	/// </summary>
	/// <remarks>This method initiates a sequence of animations on the grid area when it is tapped. The animations
	/// include translating the grid, scaling it down, and fading it. The translation distance varies depending on the
	/// operating system, with a larger translation on iOS and Android platforms.</remarks>
	/// <param name="sender">The source of the event, typically the grid area that was tapped.</param>
	/// <param name="e">The event data associated with the tap event.</param>
	async void GridArea_Tapped(object? sender, EventArgs? e)
	{
		ViewModel.Press();
		if (isMenuOpen)
		{
			isMenuOpen = false;
			ViewModel.IsReaderModeEnabled = false;
			sliderContainer.IsVisible = false;
			await grid.ScaleToAsync(1, animationDuration);
			await grid.FadeToAsync(1, animationDuration);
			return;
		}
		isMenuOpen = true;
		sliderContainer.IsVisible = true;
		ViewModel.IsReaderModeEnabled = true;
		await grid.ScaleToAsync(0.8, animationDuration);
		await grid.FadeToAsync(0.8, animationDuration);
	}

	/// <summary>
	/// Handles the navigation event for the web view.
	/// </summary>
	/// <remarks>This method is triggered after the web view has completed navigation to a new page. It updates the
	/// UI to reflect the navigation state.</remarks>
	/// <param name="sender">The source of the event, typically the web view control.</param>
	/// <param name="e">The event data containing information about the navigation event.</param>
	async void webView_Navigated(object? sender, WebNavigatedEventArgs? e)
	{
		await Dispatcher.DispatchAsync(async () =>
		{
			Trace.TraceInformation($"[PageRestore] webView_Navigated: book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
			var pageLoaded = await LoadAndMergeProgressAsync(ViewModel.CancellationTokenSource.Token);
			Trace.TraceInformation($"[PageRestore] webView_Navigated: LoadAndMergeProgress returned pageLoaded={pageLoaded}; book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
			if (!pageLoaded)
			{
				Trace.TraceInformation("[PageRestore] webView_Navigated: calling LoadChapterContentAsync (pageLoaded=false path)");
				await LoadChapterContentAsync();
			}
		});
	}

	async Task StartLoadSequenceAsync()
	{
		Dispatcher.Dispatch(() =>
		{
			webView.Source = ViewModel.Source;
		});
	}

	/// <summary>
	/// Handles the Loaded event for the current page, initializing toolbar items for each chapter in the book.
	/// </summary>
	/// <param name="sender">The source of the event.</param>
	/// <param name="e">The event data.</param>
	async void CurrentPage_Loaded(object? sender, EventArgs? e)
	{
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
     pageSlider.Minimum = 0;
		pageSlider.Maximum = 0;
		pageSlider.Value = 0;
	}

	/// <summary>
	/// Asynchronously handles a JavaScript action based on the provided URL.
	/// </summary>
	/// <remarks>This method processes the URL to determine the appropriate JavaScript action and updates the UI
	/// accordingly. It attempts to handle both internal and external links before executing a specific web view
	/// action.</remarks>
	/// <param name="url">The URL that determines the JavaScript action to be handled. Cannot be null or empty.</param>
	/// <returns></returns>
	async Task HandleJavascriptAsync(string url)
	{
		if (!string.IsNullOrEmpty(url) && url.TrimStart().StartsWith('{'))
		{
			if (BookPageJsMessage.TryParse(url, out var msg))
			{
				var resolved = msg.ToRuncsharpUrl();
				if (!string.IsNullOrEmpty(resolved))
				{
					url = resolved;
				}
			}
			else
			{
				Trace.TraceWarning("Failed parsing JSON message from JS bridge");
			}
		}
		await TryHandleInternalLinkAsync(url);
		await BookPage.TryHandleExternalLinkAsync(url);
		var methodName = GetMethodNameFromUrl(url);
		var data = BookPage.GetDataFromUrl(url);
		await HandleWebViewActionAsync(methodName, data);
		await UpdateUiAppearance();
	}

	/// <summary>
	/// Handles the navigation event for the web view, intercepting specific URLs for custom processing.
	/// </summary>
	/// <remarks>This method cancels navigation if the URL contains the substring "runcsharp", ignoring case, and
	/// processes the URL asynchronously.</remarks>
	/// <param name="sender">The source of the event, typically the web view control.</param>
	/// <param name="e">The <see cref="WebNavigatingEventArgs"/> containing event data, including the URL being navigated to.</param>
	async void webView_Navigating(object? sender, WebNavigatingEventArgs? e)
	{
		if (e is null)
		{
			return;
		}
		var url = e.Url;

		if (!url.Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			return;
		}

		e.Cancel = true;
		await HandleJavascriptAsync(e.Url);
	}

	static string GetDataFromUrl(string url)
	{
		var parts = url.Split('?');
		if (parts.Length > 1)
		{
			var data = parts[1].Split('#')[0];
			return data;
		}
		return string.Empty;
	}

	async Task TryHandleInternalLinkAsync(string url)
	{
		if (!url.Contains("https://runcsharp.jump?https://demo/", StringComparison.InvariantCultureIgnoreCase))
		{
			return;
		}
		var urlParts = url.Split('?')[1].Split('#')[0];
		if (!string.IsNullOrEmpty(urlParts))
		{
			if (urlParts.Contains("https://demo/"))
			{
				urlParts = urlParts.Replace("https://demo/", string.Empty, StringComparison.OrdinalIgnoreCase);
			}

			var chapter = book.Chapters.Find(chapter => chapter.FileName.Contains(Path.GetFileName(urlParts), StringComparison.OrdinalIgnoreCase));
			if (chapter is null)
			{
				System.Diagnostics.Trace.TraceWarning("Chapter not found for internal link");
				return;
			}
			System.Diagnostics.Trace.TraceInformation($"Found chapter: {chapter.Title}");
			book.CurrentChapter = book.Chapters.IndexOf(chapter);
			// Use helper to ensure native and media overlay state stay in sync
			await LoadChapterContentAsync();
			await SaveProgressAsync(ViewModel.CancellationTokenSource.Token);
		}
	}

	static async Task TryHandleExternalLinkAsync(string url)
	{
		if (!url.Contains(externalLinkPrefix, StringComparison.OrdinalIgnoreCase) || url.Contains("https://demo") || url.Contains("app://demo"))
		{
			return;
		}
		var urlParts = url.Split('?');
		if (urlParts.Length <= 1)
		{
			return;
		}

		var queryString = urlParts[1].Replace("http://", "https://");
		if (!string.IsNullOrEmpty(queryString) && queryString.Contains("https://"))
		{
			System.Diagnostics.Trace.TraceInformation($"Opening external link: {queryString}");
			await Launcher.OpenAsync(queryString);
		}
	}

	static string GetMethodNameFromUrl(string url)
	{
		var urlParts = url.Split('.');
		if (urlParts.Length < 2)
		{
			return string.Empty;
		}

		var funcToCall = urlParts[1].Split('?');
		var candidate = funcToCall[0];
		if (string.IsNullOrEmpty(candidate))
		{
			return string.Empty;
		}

		// Extract a valid identifier at the start (handles "prev", "prev()", "menu()", etc.)
		var m = System.Text.RegularExpressions.Regex.Match(candidate, "[A-Za-z_][A-Za-z0-9_]*", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
		return m.Success ? m.Value : string.Empty;
	}

	async Task HandleWebViewActionAsync(string methodName, string data)
	{
		var currentPage = int.TryParse(await webView.EvaluateJavaScriptAsync("getCurrentPage()"), out int parsedPage) ? parsedPage : 0;
		var lowerMethod = methodName.ToLowerInvariant();
		switch (lowerMethod)
		{
			case "next":
				await HandleNextAsync();
				break;
			case "prev":
				System.Diagnostics.Trace.TraceInformation("Handling prev action from JS");
				await HandlePrevAsync();
				break;
			case "menu":
				HandleMenu();
				break;
			case "pageload":
				await HandlePageLoadAsync(currentPage);
				break;
			case "characterposition":
				await HandleCharacterPositionAsync(currentPage);
				break;
             case "sectionchange":
					HandleSectionChange(data);
					break;
			case "mediaoverlaylog":
				HandleMediaOverlayLog(data);
				break;
			case "mediaoverlaytoggle":
				await HandleMediaOverlayToggleAsync(data);
				break;
			case "mediaoverlayplay":
				await HandleMediaOverlayPlayAsync();
				break;
			case "mediaoverlaypause":
				await HandleMediaOverlayPauseAsync();
				break;
			case "mediaoverlaynext":
				await HandleMediaOverlayNextAsync().ConfigureAwait(false);
				break;
			case "mediaoverlayprev":
				System.Diagnostics.Trace.TraceInformation("Handling media overlay prev action from JS");
				await HandleMediaOverlayPrevAsync().ConfigureAwait(false);
				break;
			case "mediaoverlayseek":
				if (mediaOverlayManager is not null && double.TryParse(data, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var secs))
				{
					await mediaOverlayManager.SeekAsync(secs);
				}
				break;
			case "layoutoverflow":
				HandleLayoutOverflow(data);
				break;
		}
	}

	async Task HandleNextAsync()
	{
		var requiresPageLoadEvent = await webViewHelper.Next(pageLabel, book);
		await NotifyMediaOverlayChapterRequestedAsync();
		if (!requiresPageLoadEvent)
		{
			await HandlePostChapterLoadAsync();
		}
	}

	async Task HandlePrevAsync()
	{
		System.Diagnostics.Trace.TraceInformation("Handling prev action from JS");
        if (book.CurrentChapter <= 0)
		{
			navigateToChapterEndOnLoad = false;
			return;
		}

      navigateToChapterEndOnLoad = true;
		var requiresPageLoadEvent = await webViewHelper.Prev(pageLabel, book);
		await NotifyMediaOverlayChapterRequestedAsync();
		if (!requiresPageLoadEvent)
		{
         await HandlePostChapterLoadAsync();
		}
	}

	/// <summary>
	/// Applies ReadiumCSS settings and positions the page after a section-only swap in combined mode.
	/// Called instead of waiting for a <c>pageload</c> JS event when <c>showSection()</c> is used.
	/// </summary>
	async Task HandlePostChapterLoadAsync()
	{
		Trace.TraceInformation($"[PageRestore] HandlePostChapterLoadAsync: book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage} navigateToEnd={navigateToChapterEndOnLoad}");
		await Dispatcher.DispatchAsync(async () =>
		{
			await webViewHelper.OnSettingsClickedAsync();
			await UpdateUiAppearance();
		   if (navigateToChapterEndOnLoad)
			{
				navigateToChapterEndOnLoad = false;
				Trace.TraceInformation("[PageRestore] HandlePostChapterLoadAsync: scrollToHorizontalEnd (navigateToEnd path)");
				await webView.EvaluateJavaScriptAsync("scrollToHorizontalEnd()");
			}
			else if (book.CurrentPage > 0)
			{
				Trace.TraceInformation($"[PageRestore] HandlePostChapterLoadAsync: calling gotoPage({book.CurrentPage})");
				await webView.EvaluateJavaScriptAsync($"gotoPage({book.CurrentPage})");
			}
			else
			{
				Trace.TraceInformation($"[PageRestore] HandlePostChapterLoadAsync: gotoPage SKIPPED (book.Pg={book.CurrentPage})");
			}
			sliderPageLabel.Text = pageLabel.Text = await GetCurrentPageInfoAsync();
			Trace.TraceInformation($"[PageRestore] HandlePostChapterLoadAsync done: book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage} label='{pageLabel.Text}'");
			await NotifyMediaOverlayPageLoadedAsync();
		});
	}

	void HandleSectionChange(string data)
	{
		if (!int.TryParse(data, out var chapterIndex))
		{
			return;
		}

		if (chapterIndex < 0 || chapterIndex >= book.Chapters.Count)
		{
			return;
		}

		book.CurrentChapter = chapterIndex;
		book.CurrentPage = 0;
	}

	void HandleMenu()
	{
		GridArea_Tapped(this, EventArgs.Empty);
	}

	async Task HandlePageLoadAsync(int currentPage)
	{
		Trace.TraceInformation($"[PageRestore] HandlePageLoadAsync: jsCurrentPage={currentPage} book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage} navigateToEnd={navigateToChapterEndOnLoad}");
		await Dispatcher.DispatchAsync(async () =>
		{
		   webViewHelper.MarkCombinedHtmlLoaded();
			await webView.EvaluateJavaScriptAsync($"showSection({book.CurrentChapter});");
			Trace.TraceInformation($"[PageRestore] HandlePageLoadAsync: showSection({book.CurrentChapter}) called");

			await webViewHelper.OnSettingsClickedAsync();
			await UpdateUiAppearance();
		   if (navigateToChapterEndOnLoad)
			{
				navigateToChapterEndOnLoad = false;
				Trace.TraceInformation("[PageRestore] HandlePageLoadAsync: scrollToHorizontalEnd (navigateToEnd path)");
				await webView.EvaluateJavaScriptAsync("scrollToHorizontalEnd()");
			}
			else if (currentPage == 0 && book.CurrentPage > 0)
			{
				Trace.TraceInformation($"[PageRestore] HandlePageLoadAsync: calling gotoPage({book.CurrentPage})");
				await webView.EvaluateJavaScriptAsync($"gotoPage({book.CurrentPage})");
			}
			else
			{
				Trace.TraceInformation($"[PageRestore] HandlePageLoadAsync: gotoPage SKIPPED (jsCurrentPage={currentPage}, book.Pg={book.CurrentPage})");
			}
			sliderPageLabel.Text = pageLabel.Text = await GetCurrentPageInfoAsync();
			Trace.TraceInformation($"[PageRestore] HandlePageLoadAsync done: book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage} label='{pageLabel.Text}'");
			await NotifyMediaOverlayPageLoadedAsync();
		});
	}

	async Task HandleCharacterPositionAsync(int currentPage)
	{
		Trace.TraceInformation($"[PageRestore] HandleCharacterPositionAsync: jsPage={currentPage} book.Ch={book.CurrentChapter}");
		book.CurrentPage = currentPage;
		await Dispatcher.DispatchAsync(async () =>
		{
			await SaveProgressAsync(ViewModel.CancellationTokenSource.Token);
            if (sliderTotalPages <= 0 || book.CurrentChapter < 0 || book.CurrentChapter >= chapterOffsets.Count)
			{
                await RefreshPaginationStateAsync(ViewModel.CancellationTokenSource.Token);
			}

			if (sliderTotalPages <= 0)
			{
               return;
			}

			var globalPageNumber = GetCurrentGlobalPageNumber(currentPage);
			sliderPageLabel.Text = pageLabel.Text = WebViewHelper.FormatPageLabel(book, globalPageNumber, sliderTotalPages);

			if (!isSliderActive && sliderTotalPages > 0)
			{
				pageSlider.Value = Math.Max(pageSlider.Minimum, Math.Min(pageSlider.Maximum, globalPageNumber - 1));
			}
		});
	}

	static void HandleMediaOverlayLog(string data)
	{
		if (!string.IsNullOrEmpty(data))
		{
			var decoded = Uri.UnescapeDataString(data);
			Trace.TraceInformation($"[MediaOverlay JS] {decoded}");
		}
	}

	static void HandleLayoutOverflow(string data)
	{
		if (string.IsNullOrWhiteSpace(data))
		{
			Trace.TraceWarning("[ReaderOverflow] Received empty overflow payload from JS.");
			return;
		}

		var decoded = Uri.UnescapeDataString(data);
		Trace.TraceWarning($"[ReaderOverflow] {decoded}");
	}

	async Task HandleMediaOverlayToggleAsync(string data)
	{
		if (mediaOverlayManager is null)
		{
			Trace.TraceInformation("Media overlay toggle ignored; manager unavailable.");
			return;
		}
		if (!bool.TryParse(data, out var enabled))
		{
			Trace.TraceWarning($"Invalid media overlay toggle payload: {data}");
			return;
		}
		await mediaOverlayManager.SetEnabledAsync(enabled);
	}

	async Task HandleMediaOverlayPlayAsync()
	{
		if (mediaOverlayManager is not null)
		{
			await mediaOverlayManager.PlayAsync();
		}
	}

	async Task HandleMediaOverlayPauseAsync()
	{
		if (mediaOverlayManager is not null)
		{
			await mediaOverlayManager.PauseAsync();
		}
	}

	async Task HandleMediaOverlayNextAsync()
	{
		if (mediaOverlayManager is not null)
		{
			await mediaOverlayManager.NextAsync();
		}
	}

	async Task HandleMediaOverlayPrevAsync()
	{
		if (mediaOverlayManager is not null)
		{
			await mediaOverlayManager.PreviousAsync();
		}
	}

	async void OnMediaOverlayChapterAdvanceRequested(object? sender, EventArgs e)
	{
		await HandleNextAsync();
	}

	async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(BookViewModel.Book):
				await EnsureMediaOverlayManagerInitialized();
				break;
			case nameof(BookViewModel.IsReaderModeEnabled):
				await UpdateReaderModeOverlayAsync(ViewModel.IsReaderModeEnabled);
				break;
		}
	}

	async Task EnsureMediaOverlayManagerInitialized()
	{
		if (mediaOverlayManager is not null)
		{
			mediaOverlayManager.UpdateBook(book);
			return;
		}

		if (!book.HasNarratedMedia)
		{
			return;
		}

		mediaOverlayManager = new MediaOverlayPlaybackManager(ViewModel, webView, audioManager);
		mediaOverlayManager.PlaybackProgressChanged += OnMediaOverlayPlaybackProgressChanged;
		mediaOverlayManager.ChapterAdvanceRequested += OnMediaOverlayChapterAdvanceRequested;
		await UpdateReaderModeOverlayAsync(ViewModel.IsReaderModeEnabled);
	}

	async void OnMediaOverlayPlaybackProgressChanged(object? sender, MediaOverlayPlaybackProgress progress)
	{
		latestMediaOverlayProgress = progress;

		// Only sync progress for the currently open chapter to avoid cross-chapter confusion.
		if (progress.ChapterIndex != book.CurrentChapter)
		{
			return;
		}

		var now = DateTimeOffset.UtcNow;
		var previous = lastMediaOverlayProgressSent;

		var enabledChanged = previous is null || previous.Enabled != progress.Enabled;
		var segmentChanged = previous is null || previous.SegmentIndex != progress.SegmentIndex;
		var posChanged = (previous?.PositionSeconds is double a && progress.PositionSeconds is double b)
			? Math.Abs(a - b) >= 1
			: !(previous?.PositionSeconds is null && progress.PositionSeconds is null);

		// Sync policy:
		// - always sync enable/segment changes
		// - treat large position jumps (seek) as immediate
		// - otherwise, sync periodically while position moves
		var positionJump = (previous?.PositionSeconds is double pa && progress.PositionSeconds is double pb) && Math.Abs(pa - pb) >= 5;

		var periodicDue = now - lastMediaOverlayProgressSyncedAt >= TimeSpan.FromSeconds(10);
		var shouldSync = enabledChanged || segmentChanged || positionJump || (periodicDue && posChanged);

		if (!shouldSync)
		{
			return;
		}

		lastMediaOverlayProgressSyncedAt = now;
		lastMediaOverlayProgressSent = progress;

		await SaveProgressAsync(ViewModel.CancellationTokenSource.Token);
	}

	async Task UpdateReaderModeOverlayAsync(bool isReaderModeEnabled)
	{
		var manager = mediaOverlayManager;
		if (manager is null)
		{
			return;
		}

		await manager.SetReaderModeHiddenAsync(isReaderModeEnabled);
	}

	async Task NotifyMediaOverlayChapterRequestedAsync()
	{
		var manager = mediaOverlayManager;
		if (manager is null)
		{
			return;
		}
		await manager.OnChapterRequestedAsync(book.CurrentChapter);
	}

	async Task NotifyMediaOverlayPageLoadedAsync()
	{
		var manager = mediaOverlayManager;
		if (manager is null)
		{
			return;
		}

		await manager.OnPageLoadedAsync(book.CurrentChapter);
	}

	async Task LoadChapterContentAsync()
	{
		Trace.TraceInformation($"[PageRestore] LoadChapterContentAsync: book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
		await EnsureMediaOverlayManagerInitialized();
		await NotifyMediaOverlayChapterRequestedAsync();
		var requiresPageLoadEvent = await webViewHelper.LoadPageAsync(pageLabel, book);
		Trace.TraceInformation($"[PageRestore] LoadChapterContentAsync: requiresPageLoadEvent={requiresPageLoadEvent}");
		if (!requiresPageLoadEvent)
		{
			// Combined mode: showSection() was called — no pageload event will fire.
			// Apply settings and position the page directly here.
			await HandlePostChapterLoadAsync();
		}
		NativeLoadingOverlay.IsVisible = false;
	}

	/// <summary>
	/// Gets the current page information using synthetic page numbers.
	/// </summary>
	/// <returns>A formatted string with synthetic page information.</returns>
	async Task<string> GetCurrentPageInfoAsync()
	{
		// Guard: combined.html sections are not available until the pageload JS event fires and
		// MarkCombinedHtmlLoaded() is called. Querying JS before that returns stale defaults
		// (Ch=0, Pg=0) which would corrupt the restored book position.
		if (!webViewHelper.CombinedHtmlIsLoaded)
		{
			Trace.TraceInformation($"[PageRestore] GetCurrentPageInfoAsync: combined.html not loaded yet, returning title only (book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage})");
			return book.Chapters[book.CurrentChapter]?.Title ?? string.Empty;
		}

		var pagination = await webViewHelper.GetCombinedPaginationInfoAsync(ViewModel.CancellationTokenSource.Token);

		if (pagination is null)
		{
			Trace.TraceInformation($"[PageRestore] GetCurrentPageInfoAsync: pagination=null, returning title only (book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage})");
			return book.Chapters[book.CurrentChapter]?.Title ?? string.Empty;
		}

		Trace.TraceInformation($"[PageRestore] GetCurrentPageInfoAsync: pagination.Ch={pagination.CurrentSectionIndex} pagination.Pg={pagination.CurrentPage} pagination.Total={pagination.TotalPages} offsets.Count={pagination.ChapterOffsets.Count}");
		Trace.TraceInformation($"[PageRestore] GetCurrentPageInfoAsync: calling ApplyPaginationInfo (book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage} → will become Ch={pagination.CurrentSectionIndex} Pg={pagination.CurrentPage})");
		ApplyPaginationInfo(pagination);
		return WebViewHelper.FormatPageLabel(book, pagination.CurrentGlobalPage, pagination.TotalPages);
	}

	async Task UpdateUiAppearance()
	{
		sliderPageLabel.IsVisible = pageLabel.IsVisible = !string.IsNullOrEmpty(pageLabel.Text);
		var settings = await db.GetSettings() ?? new();
		if (string.IsNullOrEmpty(settings.BackgroundColor))
		{
			settings.BackgroundColor = "#FFFFFF"; // Default background color
			settings.TextColor = "#000000"; // Default text color
		}
		if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
		{
			grid.BackgroundColor = Color.FromArgb(settings.BackgroundColor);
			pageLabel.TextColor = Color.FromArgb(settings.TextColor);
		}
	}

	/// <summary>
	/// Asynchronously closes the menu by performing a series of animations.
	/// </summary>
	/// <remarks>This method triggers the ViewModel's press action and performs fade, scale, and translation
	/// animations on the grid element to close the menu. The animations are executed sequentially with a specified
	/// duration.</remarks>
	/// <param name="sender">The source of the event that triggered the method.</param>
	/// <param name="e">The event data associated with the event.</param>
	async void CloseMenuAsync(object? sender, EventArgs? e)
	{
		isMenuOpen = false;
		sliderContainer.IsVisible = false;
		ViewModel.Press();
		await grid.FadeToAsync(1, animationDuration);
		await grid.ScaleToAsync(1, animationDuration);
	}

	void CreateToolBarItem(int index, Chapter chapter)
	{
		ArgumentNullException.ThrowIfNull(book);
		if (string.IsNullOrEmpty(chapter.Title))
		{
			return;
		}
#if IOS || MACCATALYST
		Label label = new()
		{
			Text = chapter.Title,
			TextColor = Colors.White,
			HorizontalOptions = LayoutOptions.End,
			Margin = new Thickness(0, 0, 10, 0),
		};
		label.GestureRecognizers.Add(new TapGestureRecognizer
		{
			Command = new Command(() =>
			{
				Dispatcher.Dispatch(async () =>
				{
					book.CurrentChapter = index;
					book.CurrentPage = 0; // Reset current page to 0 when changing chapter
					await SaveProgressAsync(ViewModel.CancellationTokenSource.Token);
					await LoadChapterContentAsync();
					CloseMenuAsync(this, EventArgs.Empty);
				});
			})
		});

		menu.Add(label);
		menu.SetRow(label, index);
		menu.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1, GridUnitType.Auto)
		});
#else
		var currentShell = Shell.Current;
		if (currentShell is null)
		{
			return;
		}

        var toolbarItem = new ToolbarItem
        {
            Text = chapter.Title,
            Order = ToolbarItemOrder.Secondary,
            Priority = index,
            Command = new Command(() =>
            {
                Dispatcher.Dispatch(async () =>
                {
                    book.CurrentChapter = index;
                    book.CurrentPage = 0; // Reset current page when changing chapter
                    await SaveProgressAsync(ViewModel.CancellationTokenSource.Token);
                        await LoadChapterContentAsync();
                    CloseMenuAsync(this, EventArgs.Empty);
                });
            })
        };
		currentShell.ToolbarItems.Add(toolbarItem);
#endif
	}

	// --- Slider helpers and event handlers ---

	void ApplyPaginationInfo(WebViewHelper.CombinedPaginationInfo pagination)
	{
		Trace.TraceInformation($"[PageRestore] ApplyPaginationInfo: BEFORE book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
		chapterOffsets.Clear();
	  chapterOffsets.AddRange(pagination.ChapterOffsets);
		sliderTotalPages = pagination.TotalPages;
		if (pagination.CurrentSectionIndex >= 0 && pagination.CurrentSectionIndex < book.Chapters.Count)
		{
		 book.CurrentChapter = pagination.CurrentSectionIndex;
		}

		book.CurrentPage = pagination.CurrentPage;
		Trace.TraceInformation($"[PageRestore] ApplyPaginationInfo: AFTER  book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage} totalPages={sliderTotalPages}");

		pageSlider.Minimum = 0;
		pageSlider.Maximum = Math.Max(0, sliderTotalPages - 1);
		if (!isSliderActive && sliderTotalPages > 0)
		{
			pageSlider.Value = Math.Max(pageSlider.Minimum, Math.Min(pageSlider.Maximum, pagination.CurrentGlobalPage - 1));
		}
		else if (sliderTotalPages == 0)
		{
			pageSlider.Value = 0;
		}
	}

	int GetCurrentGlobalPageNumber(int currentPage)
	{
		if (sliderTotalPages <= 0)
		{
			return Math.Max(1, currentPage + 1);
		}

		if (book.CurrentChapter < 0 || book.CurrentChapter >= chapterOffsets.Count)
		{
			return Math.Max(1, currentPage + 1);
		}

		var offset = chapterOffsets[book.CurrentChapter];
		return Math.Min(sliderTotalPages, offset + Math.Max(0, currentPage) + 1);
	}

	async Task RefreshPaginationStateAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		if (!webViewHelper.CombinedHtmlIsLoaded)
		{
			return;
		}

		var pagination = await webViewHelper.GetCombinedPaginationInfoAsync(token);
		if (pagination is null)
		{
			return;
		}

		ApplyPaginationInfo(pagination);
		sliderPageLabel.Text = pageLabel.Text = WebViewHelper.FormatPageLabel(book, pagination.CurrentGlobalPage, pagination.TotalPages);
	}

	int MapGlobalPageToChapter(int globalIndex, out int localPage)
	{
		localPage = 0;
		if (sliderTotalPages <= 0)
		{
			return book.CurrentChapter;
		}

		// Clamp
		var g = Math.Max(0, Math.Min(globalIndex, sliderTotalPages - 1));

		for (int i = 0; i < chapterOffsets.Count; i++)
		{
			var start = chapterOffsets[i];
			var nextStart = (i + 1 < chapterOffsets.Count) ? chapterOffsets[i + 1] : sliderTotalPages;
			if (g >= start && g < nextStart)
			{
				localPage = g - start;
				return i;
			}
		}
		// fallback to last chapter
		var last = chapterOffsets.Count - 1;
		localPage = g - chapterOffsets[last];
		return last;
	}

	async void PageSlider_DragStarted(object? sender, EventArgs e)
	{
		try
		{
			isSliderActive = true;
			// Make webview non-interactive on both native and JS sides
			Dispatcher.Dispatch(() =>
			{
				webView.InputTransparent = true;
				webView.IsEnabled = false;
			});

			// Inject small JS wrapper to force instant scroll during sliding and call setInteractionEnabled(false)
			// This keeps the change transient (we restore on DragCompleted)
			var js = @"
                try {
                    window.__sliderInstant = true;
                    if (typeof navigationUtils !== 'undefined' && !window.__origAnimateTo) {
                        window.__origAnimateTo = navigationUtils.animateTo;
                        navigationUtils.animateTo = function(contentWindow, targetLeft, platform) {
                            try { contentWindow.scrollTo(targetLeft, 0); } catch(e) {}
                            return Promise.resolve();
                        };
                    }
                    if (typeof setInteractionEnabled === 'function') {
                        try { setInteractionEnabled(false); } catch(e) {}
                    }
                } catch(e) { console.warn('slider start inject failed', e); }
            ";
			await webView.EvaluateJavaScriptAsync(js);
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"PageSlider_DragStarted failed: {ex.Message}");
		}
	}

	async void PageSlider_ValueChanged(object? sender, ValueChangedEventArgs e)
	{
		if (!isSliderActive)
		{
			// only act when slider is being actively dragged
			return;
		}
		try
		{
			var targetGlobal = (int)Math.Round(e.NewValue);

			var targetChapter = MapGlobalPageToChapter(targetGlobal, out var localPage);

			if (targetChapter == book.CurrentChapter)
			{
				// Fast in-chapter move: use JS gotoPage for instant navigation
				await webView.EvaluateJavaScriptAsync($"gotoPage({localPage});");
				// update local model so label and save logic remain consistent
				book.CurrentPage = localPage;
                var globalPageNumber = GetCurrentGlobalPageNumber(localPage);
				sliderPageLabel.Text = pageLabel.Text = WebViewHelper.FormatPageLabel(book, globalPageNumber, sliderTotalPages);
			}
			else
			{
				// Changing chapter: update model and load that chapter (existing LoadChapterContentAsync will cause JS to goto page)
				book.CurrentChapter = targetChapter;
				book.CurrentPage = localPage;
				// Do not SaveProgress for each intermediate change (too frequent) — Save on DragCompleted
				await LoadChapterContentAsync();
			}
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"PageSlider_ValueChanged failed: {ex.Message}");
		}
	}

	async void PageSlider_DragCompleted(object? sender, EventArgs e)
	{
		try
		{
			isSliderActive = false;
			// restore native interaction
			Dispatcher.Dispatch(() =>
			{
				webView.InputTransparent = false;
				webView.IsEnabled = true;
			});

			// Restore original animateTo and re-enable interaction in JS
			var js = @"
                try {
                    window.__sliderInstant = false;
                    if (typeof navigationUtils !== 'undefined' && window.__origAnimateTo) {
                        navigationUtils.animateTo = window.__origAnimateTo;
                        window.__origAnimateTo = null;
                    }
                    if (typeof setInteractionEnabled === 'function') {
                        try { setInteractionEnabled(true); } catch(e) {}
                    }
                } catch(e) { console.warn('slider end inject failed', e); }
            ";
			await webView.EvaluateJavaScriptAsync(js);

			// Persist final position
			await SaveProgressAsync(ViewModel.CancellationTokenSource.Token);
            await RefreshPaginationStateAsync(ViewModel.CancellationTokenSource.Token);
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"PageSlider_DragCompleted failed: {ex.Message}");
		}
	}

	async Task<bool> LoadAndMergeProgressAsync(CancellationToken token)
	{
		var pageLoaded = false;
		try
		{
			token.ThrowIfCancellationRequested();

			Trace.TraceInformation($"[PageRestore] LoadAndMergeProgressAsync: ENTER book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
			var bookId = await BookIdentityService.ComputeSyncIdAsync(book, token);
			var cloud = await syncService.GetProgressAsync(bookId, token);

			if (cloud is null)
			{
				Trace.TraceInformation("[PageRestore] LoadAndMergeProgressAsync: cloud=null, using local-only path");
				await PrimeLocalMediaOverlayRestoreAsync();
				await BackfillLegacyProgressIfNeededAsync(bookId, token);
			}
			else
			{
				Trace.TraceInformation($"[PageRestore] LoadAndMergeProgressAsync: cloud found Ch={cloud.CurrentChapter} Pg={cloud.CurrentPage}");
				pageLoaded = await ResolveProgressAsync(cloud, token);
			}

			Trace.TraceInformation($"[PageRestore] LoadAndMergeProgressAsync: before GetCurrentPageInfoAsync book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
			sliderPageLabel.Text = pageLabel.Text = await GetCurrentPageInfoAsync();
			Trace.TraceInformation($"[PageRestore] LoadAndMergeProgressAsync: EXIT pageLoaded={pageLoaded} book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
			return pageLoaded;
		}
		catch (OperationCanceledException ex)
		{
			System.Diagnostics.Trace.TraceInformation($"Progress load cancelled: {ex.Message}");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Trace.TraceError($"Failed to load progress: {ex.Message}");
		}
		return pageLoaded;
	}

	async Task BackfillLegacyProgressIfNeededAsync(string bookId, CancellationToken token)
	{
		if (book.CurrentChapter <= 0 && book.CurrentPage <= 0)
		{
			return;
		}
		var progress = new ReadingProgress
		{
			BookId = bookId,
			CurrentChapter = book.CurrentChapter,
			CurrentPage = book.CurrentPage,
			LastUpdated = DateTimeOffset.UtcNow.ToString("o"),
			DeviceId = string.Empty,
			DeviceName = string.Empty,
			IsSynced = false
		};
		await syncService.SaveProgressAsync(progress, token);
	}

	async Task<bool> ResolveProgressAsync(ReadingProgress cloud, CancellationToken token)
	{
		if (cloud.CurrentChapter == book.CurrentChapter && cloud.CurrentPage == book.CurrentPage)
		{
			await ApplyProgressToUiAsync(cloud);
			return true;
		}

		var (localInfo, cloudInfo) = BuildProgressComparisonStrings(cloud);
		var moveToCloud = await AskMoveToCloudAsync(localInfo, cloudInfo);
		if (moveToCloud)
		{
			await ApplyProgressToUiAsync(cloud);
			await TryShowInfoAsync("Moved to latest synced position");
			return true;
		}

		await SaveProgressAsync(token);
		await TryShowInfoAsync("Saved local progress to cloud");
		return false;
	}

	(string localInfo, string cloudInfo) BuildProgressComparisonStrings(ReadingProgress cloud)
	{
		var localChapterTitle = (book.CurrentChapter >= 0 && book.CurrentChapter < book.Chapters.Count)
			? book.Chapters[book.CurrentChapter].Title
			: "Unknown";
		var cloudChapterTitle = (cloud.CurrentChapter >= 0 && cloud.CurrentChapter < book.Chapters.Count)
			? book.Chapters[cloud.CurrentChapter].Title
			: "Unknown";

        var localInfo = $"{localChapterTitle} — Page {Math.Max(1, book.CurrentPage + 1)}";
		var cloudInfo = $"{cloudChapterTitle} — Page {Math.Max(1, cloud.CurrentPage + 1)}";
		return (localInfo, cloudInfo);
	}

	async Task<bool> AskMoveToCloudAsync(string localInfo, string cloudInfo)
	{
		return await Dispatcher.DispatchAsync(async () =>
			await DisplayAlertAsync(
				"Synced position found",
				$"A different synced position was found for this book.\n\nCurrent (local): {localInfo}\nRemote (synced): {cloudInfo}\n\nMove to the remote position?",
				"Move",
				"Keep Local"
			)
		);
	}

	async Task TryShowInfoAsync(string message)
	{
		try
		{
			await ViewModel.ShowInfoToastAsync(message);
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"ShowInfoToastAsync failed: {ex.Message}");
		}
	}

	async Task SaveProgressAsync(CancellationToken token)
	{
		// Persist the local book position to the local database first so we can always
		// distinguish local vs cloud positions.
		try
		{
			// Update only the progress columns to avoid overwriting other book fields
			await db.UpdateBookProgress(book.Id, book.CurrentChapter, book.CurrentPage, token);
			System.Diagnostics.Trace.TraceInformation($"Saved progress locally: Chapter {book.CurrentChapter}, Page {book.CurrentPage}");
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed updating local book progress in DB: {ex.Message}");
		}

		// Update the book's LastOpenedDate to track recent reads
		try
		{
			book.LastOpenedDate = DateTime.UtcNow;
			await db.SaveBookData(book, token);
			System.Diagnostics.Trace.TraceInformation($"Updated book LastOpenedDate to {book.LastOpenedDate:o}");
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed updating book LastOpenedDate: {ex.Message}");
		}

		// Persist Media Overlay playback state to the main Book DB for local-only users.
		try
		{
			var localMo = latestMediaOverlayProgress;
			if (localMo is not null && localMo.ChapterIndex == book.CurrentChapter)
			{
				book.MediaOverlayEnabled = localMo.Enabled;
				book.MediaOverlayChapter = localMo.ChapterIndex;
				book.MediaOverlaySegmentIndex = localMo.SegmentIndex;
				book.MediaOverlayPositionSeconds = localMo.PositionSeconds;
				book.MediaOverlayFragmentId = localMo.FragmentId;

				await db.UpdateBookMediaOverlayProgress(
					book.Id,
					localMo.Enabled,
					localMo.ChapterIndex,
					localMo.SegmentIndex,
					localMo.PositionSeconds,
					localMo.FragmentId,
					token);
			}
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed updating local media overlay progress in DB: {ex.Message}");
		}

		var syncId = await BookIdentityService.ComputeSyncIdAsync(book, token);
		var progress = new ReadingProgress
		{
			BookId = syncId,
			CurrentChapter = book.CurrentChapter,
			CurrentPage = book.CurrentPage,
			LastUpdated = DateTimeOffset.UtcNow.ToString("o"),
			DeviceId = string.Empty,
			DeviceName = string.Empty,
			IsSynced = false,
			DateAdded = book.DateAdded.ToString("o"),
			LastOpenedDate = book.LastOpenedDate?.ToString("o")
		};

		// Include Media Overlay playback state when available.
		var mo = latestMediaOverlayProgress;
		if (mo is not null && mo.ChapterIndex == book.CurrentChapter)
		{
			progress.MediaOverlayEnabled = mo.Enabled;
			progress.MediaOverlayChapter = mo.ChapterIndex;
			progress.MediaOverlaySegmentIndex = mo.SegmentIndex;
			progress.MediaOverlayPositionSeconds = mo.PositionSeconds;
			progress.MediaOverlayFragmentId = mo.FragmentId;
		}
		await syncService.SaveProgressAsync(progress, token);
	}

	async Task PrimeLocalMediaOverlayRestoreAsync()
	{
		// Local persistence uses the Book table. This is also used when cloud sync
		// is disabled or no cloud progress exists.
		if (book.MediaOverlayEnabled is not bool enabled || book.MediaOverlayChapter is not int chapter)
		{
			return;
		}

		// Keep restore scoped to the current chapter (same as cloud restore).
		if (chapter != book.CurrentChapter)
		{
			return;
		}

		await EnsureMediaOverlayManagerInitialized();
		var restore = new MediaOverlayPlaybackProgress(
			Enabled: enabled,
			ChapterIndex: chapter,
			SegmentIndex: Math.Max(0, book.MediaOverlaySegmentIndex ?? 0),
			PositionSeconds: book.MediaOverlayPositionSeconds,
			FragmentId: book.MediaOverlayFragmentId);
		latestMediaOverlayProgress = restore;
		mediaOverlayManager?.SetPendingRestore(restore);
	}

	async Task ApplyProgressToUiAsync(ReadingProgress progress)
	{
		Trace.TraceInformation($"[PageRestore] ApplyProgressToUiAsync: applying Ch={progress.CurrentChapter} Pg={progress.CurrentPage} (was book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage})");
		book.CurrentChapter = progress.CurrentChapter;
		book.CurrentPage = progress.CurrentPage;

		// Prime Media Overlay restore before loading the chapter so the manager can
		// apply it on the next page load (restoring timeline + highlight).
		if (progress.MediaOverlayEnabled is bool moEnabled && progress.MediaOverlayChapter is int moChapter && moChapter == book.CurrentChapter)
		{
			await EnsureMediaOverlayManagerInitialized();
			var restore = new MediaOverlayPlaybackProgress(
				Enabled: moEnabled,
				ChapterIndex: moChapter,
				SegmentIndex: Math.Max(0, progress.MediaOverlaySegmentIndex ?? 0),
				PositionSeconds: progress.MediaOverlayPositionSeconds,
				FragmentId: progress.MediaOverlayFragmentId);
			latestMediaOverlayProgress = restore;
			mediaOverlayManager?.SetPendingRestore(restore);

			// Persist the restore state locally so local-only users get it too.
			try
			{
				book.MediaOverlayEnabled = moEnabled;
				book.MediaOverlayChapter = moChapter;
				book.MediaOverlaySegmentIndex = restore.SegmentIndex;
				book.MediaOverlayPositionSeconds = restore.PositionSeconds;
				book.MediaOverlayFragmentId = restore.FragmentId;

				await db.UpdateBookMediaOverlayProgress(
					book.Id,
					moEnabled,
					moChapter,
					restore.SegmentIndex,
					restore.PositionSeconds,
					restore.FragmentId,
					ViewModel.CancellationTokenSource.Token);
			}
			catch (Exception ex)
			{
				Trace.TraceWarning($"Failed persisting restored media overlay state: {ex.Message}");
			}
		}

		if (book.Chapters.Count > book.CurrentChapter && book.CurrentChapter >= 0)
		{
			Trace.TraceInformation($"[PageRestore] ApplyProgressToUiAsync: calling LoadChapterContentAsync Ch={book.CurrentChapter} Pg={book.CurrentPage}");
			await LoadChapterContentAsync();
			Trace.TraceInformation($"[PageRestore] ApplyProgressToUiAsync: LoadChapterContentAsync done, book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
		}

		Trace.TraceInformation($"[PageRestore] ApplyProgressToUiAsync: before GetCurrentPageInfoAsync book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
		sliderPageLabel.Text = pageLabel.Text = await GetCurrentPageInfoAsync();
		Trace.TraceInformation($"[PageRestore] ApplyProgressToUiAsync: EXIT book.Ch={book.CurrentChapter} book.Pg={book.CurrentPage}");
	}

	async Task SyncProgressNowAsync(CancellationToken token)
	{
		try
		{
			if (syncService.IsLocalOnly)
			{
				await ViewModel.ShowInfoToastAsync("Sync unavailable in local-only mode");
				return;
			}

			var syncId = await BookIdentityService.ComputeSyncIdAsync(book, token);
			var cloud = await syncService.GetCloudProgressAsync(syncId, token);
			var local = await syncService.GetLocalProgressAsync(syncId, token);

			var remoteTime = TryParseTimestamp(cloud?.LastUpdated);
			var localTime = TryParseTimestamp(local?.LastUpdated);

			if (cloud is not null && remoteTime > localTime)
			{
				if (cloud.CurrentChapter != book.CurrentChapter || cloud.CurrentPage != book.CurrentPage)
				{
					await ApplyProgressToUiAsync(cloud);
					await syncService.SaveProgressAsync(cloud, token);
				}
				sliderPageLabel.Text = pageLabel.Text = await GetCurrentPageInfoAsync();
				await ViewModel.ShowInfoToastAsync("Progress updated from cloud");
				return;
			}

			var currentProgress = new ReadingProgress
			{
				BookId = syncId,
				CurrentChapter = book.CurrentChapter,
				CurrentPage = book.CurrentPage,
				LastUpdated = DateTimeOffset.UtcNow.ToString("o"),
				DeviceId = string.Empty,
				DeviceName = string.Empty,
				IsSynced = false
			};

			if (local is null || local.CurrentChapter != book.CurrentChapter || local.CurrentPage != book.CurrentPage)
			{
				await syncService.SaveProgressAsync(currentProgress, token);
				await ViewModel.ShowInfoToastAsync("Progress synced to cloud");
			}
			else
			{
				await ViewModel.ShowInfoToastAsync("Progress already up to date");
			}

			sliderPageLabel.Text = pageLabel.Text = await GetCurrentPageInfoAsync();
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Manual sync failed: {ex.Message}");
			await ViewModel.ShowErrorToastAsync("Sync failed. Please try again.");
		}
	}

	static DateTimeOffset TryParseTimestamp(string? timestamp)
	{
		return DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
			? parsed
			: DateTimeOffset.MinValue;
	}

	/// <summary>
	/// Dispose pattern implementation. Releases managed resources when disposing is true.
	/// </summary>
	/// <param name="disposing">If true, dispose managed resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposedValue)
		{
			return;
		}

		if (disposing)
		{
			WeakReferenceMessenger.Default.UnregisterAll(this);
			CancelPendingSettingsRefresh();

			webView.Navigated -= webView_Navigated;
			webView.Navigating -= webView_Navigating;
			ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

			if (mediaOverlayManager is not null)
			{
				mediaOverlayManager.PlaybackProgressChanged -= OnMediaOverlayPlaybackProgressChanged;
				mediaOverlayManager.ChapterAdvanceRequested -= OnMediaOverlayChapterAdvanceRequested;
				mediaOverlayManager.Dispose();
			}
			mediaOverlayManager = null;
		}
		disposedValue = true;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}