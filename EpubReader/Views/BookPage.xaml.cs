namespace EpubReader.Views;

using System.Diagnostics;
using System.Globalization;
using EpubReader.Service;
using EpubReader.Util;

/// <summary>
/// Represents a page in a book application, providing functionality for displaying and interacting with book content.
/// </summary>
/// <remarks>The <see cref="BookPage"/> class is responsible for managing the user interface and interactions for
/// a single page of a book. It handles navigation, animations, and JavaScript interactions within a web view. The page
/// is initialized with a view model and a database interface, which are used for data binding and data operations,
/// respectively.</remarks>
public partial class BookPage : ContentPage
{
	const string externalLinkPrefix = "https://runcsharp.jump?";
	const uint animationDuration = 200u;
	BookViewModel ViewModel => (BookViewModel)BindingContext;
	Book book => ViewModel.Book;
	readonly IDb db;
	readonly ISyncService syncService;
	readonly WebViewHelper webViewHelper;
	bool loadSequenceStarted = false;
	bool progressSyncComplete = false;
	bool pageSettingsApplied = false;
	bool pageAtCorrectPosition = false;
	ToolbarItem? syncToolbarItem;

	/// <summary>
	/// Initializes a new instance of the <see cref="BookPage"/> class with the specified view model and database.
	/// </summary>
	/// <remarks>This constructor sets up the page's data binding context and initializes necessary components. It
	/// also registers message handlers for JavaScript and settings messages using a weak reference messenger.</remarks>
	/// <param name="viewModel">The view model that provides data binding for the page.</param>
	/// <param name="db">The database interface used for data operations within the page.</param>
	/// <param name="syncService">The sync service for managing reading progress synchronization.</param>
	public BookPage(BookViewModel viewModel, IDb db, ISyncService syncService)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		this.syncService = syncService;
		webViewHelper = new(webView, db, syncService);
		
		// Subscribe to webview helper lifecycle events to show/hide native overlay
		webViewHelper.PageLoadStarted += OnWebViewPageLoadStarted;
		webViewHelper.SettingsApplied += OnWebViewSettingsApplied;
		
		Dispatcher.Dispatch(async () => await UpdateSyncToolbarAsync());

		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, async (r, m) => await HandleJavascriptAsync(m.Value));
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, async (r, m) => { await webViewHelper.OnSettingsClickedAsync(); await UpdateUiAppearance(); });
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await UpdateSyncToolbarAsync().ConfigureAwait(false);
		if (!loadSequenceStarted)
		{
			loadSequenceStarted = true;
			progressSyncComplete = false;
			pageSettingsApplied = false;
			pageAtCorrectPosition = false;
			ShowNativeOverlay();
			// Attach webview handlers now that page is visible
			try
			{
				webView.Navigated -= webView_Navigated;
				webView.Navigating -= webView_Navigating;
				webView.Navigated += webView_Navigated;
				webView.Navigating += webView_Navigating;
			}
			catch (Exception ex)
			{
				Trace.TraceWarning($"Failed attaching webview handlers: {ex.Message}");
			}
			await StartLoadSequenceAsync().ConfigureAwait(false);
		}
	}

	async Task UpdateSyncToolbarAsync()
	{
		RemoveSyncToolbarItem();

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
		Shell.Current.ToolbarItems.Add(syncToolbarItem);
	}

	void RemoveSyncToolbarItem()
	{
		if (syncToolbarItem is not null && Shell.Current.ToolbarItems.Contains(syncToolbarItem))
		{
			Shell.Current.ToolbarItems.Remove(syncToolbarItem);
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
		if (!ViewModel.isPopupActive)
		{
			WeakReferenceMessenger.Default.UnregisterAll(this);
			Shell.Current.ToolbarItems.Clear();
			Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
			// Reset load sequence when the page is truly disappearing (not just a popup)
			loadSequenceStarted = false;
			progressSyncComplete = false;
			pageSettingsApplied = false;
			pageAtCorrectPosition = false;
		}

		// Unsubscribe events
		webViewHelper.PageLoadStarted -= OnWebViewPageLoadStarted;
		webViewHelper.SettingsApplied -= OnWebViewSettingsApplied;
		// detach webview handlers we attached on appearing
		try
		{
			webView.Navigated -= webView_Navigated;
			webView.Navigating -= webView_Navigating;
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed detaching webview handlers: {ex.Message}");
		}
		base.OnDisappearing();
	}

	void OnWebViewPageLoadStarted()
	{
		Dispatcher.Dispatch(() =>
		{
			ShowNativeOverlay();
		});
	}

	void OnWebViewSettingsApplied()
	{
		// Mark that webview settings / rendering work completed and try to hide overlay
		pageSettingsApplied = true;
		if (progressSyncComplete && pageAtCorrectPosition)
		{
			Dispatcher.Dispatch(() => HideNativeOverlay());
		}
	}

	void ShowNativeOverlay()
	{
		NativeLoadingOverlay.IsVisible = true;
	}

	void HideNativeOverlay()
	{
		NativeLoadingOverlay.IsVisible = false;
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
		var width = this.Width * 0.4;
		if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
		{
			width = this.Width * 0.8;
		}
		await grid.TranslateToAsync(-width, this.Height * 0.1, animationDuration, Easing.CubicIn).ConfigureAwait(false);
		await grid.ScaleToAsync(0.8, animationDuration).ConfigureAwait(false);
		await grid.FadeToAsync(0.8, animationDuration).ConfigureAwait(false);
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
		var pageLoaded = await LoadAndMergeProgressAsync(ViewModel.CancellationTokenSource.Token).ConfigureAwait(false);
		if (!pageLoaded)
		{
			await webViewHelper.LoadPageAsync(pageLabel, book).ConfigureAwait(false);
		}
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
	void CurrentPage_Loaded(object? sender, EventArgs? e)
	{
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
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
					Trace.TraceInformation($"Parsed JS action: {msg.Action}");
					Trace.TraceInformation($"Converted JS action to URL: {url}");
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
			System.Diagnostics.Trace.TraceInformation("Not an internal link to handle");
			return;
		}
		var urlParts = url.Split('?')[1].Split('#')[0];
				if (!string.IsNullOrEmpty(urlParts))
				{
					if(urlParts.Contains("https://demo/"))
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
					// Use helper to ensure native overlay is shown
					await webViewHelper.LoadPageAsync(pageLabel, book);
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
		var currentPage = int.TryParse(await webView.EvaluateJavaScriptAsync("getCurrentPage()"),
			out int parsedPage) ? parsedPage : 0;
		switch (methodName.ToLowerInvariant())
		{
			case "next":
				await webViewHelper.Next(pageLabel, book);
				break;
			case "prev":
				await webViewHelper.Prev(pageLabel, book);
				break;
			case "menu":
				GridArea_Tapped(this, EventArgs.Empty);
				break;
			case "pageload":
				await webViewHelper.OnSettingsClickedAsync();
				await UpdateUiAppearance();
				if (currentPage == 0 && book.CurrentPage > 0)
				{
					await webView.EvaluateJavaScriptAsync($"gotoPage({book.CurrentPage})");
				}
				pageLabel.Text = await GetCurrentPageInfoAsync();
				pageAtCorrectPosition = true;
				Dispatcher.Dispatch(() => HideNativeOverlay());
				break;
			case "characterposition":
				book.CurrentPage = currentPage;
				await SaveProgressAsync(ViewModel.CancellationTokenSource.Token);

				if (int.TryParse(data, out int characterPosition) && characterPosition > 0)
				{
					pageLabel.Text = WebViewHelper.GetSyntheticPageInfo(book, characterPosition);
				}
				else
				{
					pageLabel.Text = WebViewHelper.GetSyntheticPageInfo(book);
				}
				break;
		}
	}

	/// <summary>
	/// Gets the current page information using synthetic page numbers.
	/// </summary>
	/// <returns>A formatted string with synthetic page information.</returns>
	async Task<string> GetCurrentPageInfoAsync()
	{
		var tempPosition = await webView.EvaluateJavaScriptAsync("getCharacterPositionFromScroll()");
		if (int.TryParse(tempPosition, out int characterPosition) && characterPosition > 0)
		{
			return WebViewHelper.GetSyntheticPageInfo(book, characterPosition);
		}
		return WebViewHelper.GetSyntheticPageInfo(book);
	}

	async Task UpdateUiAppearance()
	{
		pageLabel.IsVisible = !string.IsNullOrEmpty(pageLabel.Text);
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
		ViewModel.Press();
		await grid.FadeToAsync(1, animationDuration).ConfigureAwait(false);
		await grid.ScaleToAsync(1, animationDuration).ConfigureAwait(false);
		await grid.TranslateToAsync(0, 0, animationDuration, Easing.CubicIn).ConfigureAwait(false);
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
						await webViewHelper.LoadPageAsync(pageLabel, book);
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
						await webViewHelper.LoadPageAsync(pageLabel, book);
                    CloseMenuAsync(this, EventArgs.Empty);
                });
            })
        };
        Shell.Current.ToolbarItems.Add(toolbarItem);
#endif
	}

	async Task<bool> LoadAndMergeProgressAsync(CancellationToken token)
	{
		var pageLoaded = false;
		try
		{
			token.ThrowIfCancellationRequested();
			var bookId = await BookIdentityService.ComputeSyncIdAsync(book, token);
			var cloud = await syncService.GetProgressAsync(bookId, token);

			if (cloud is null)
			{
				await BackfillLegacyProgressIfNeededAsync(bookId, token);
			}
			else
			{
				pageLoaded = await ResolveProgressAsync(cloud, token);
			}

			pageLabel.Text = await GetCurrentPageInfoAsync();
			progressSyncComplete = true;
			Dispatcher.Dispatch(() => HideNativeOverlay());
			return pageLoaded;
		}
		catch (OperationCanceledException)
		{
			// ignore
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

		var tempLocal = new Book
		{
			Chapters = book.Chapters,
			CurrentChapter = book.CurrentChapter,
			CurrentPage = book.CurrentPage
		};
		var tempCloud = new Book
		{
			Chapters = book.Chapters,
			CurrentChapter = cloud.CurrentChapter,
			CurrentPage = cloud.CurrentPage
		};

		var localSynthetic = WebViewHelper.GetSyntheticPageInfo(tempLocal);
		var cloudSynthetic = WebViewHelper.GetSyntheticPageInfo(tempCloud);

		var localInfo = $"{localChapterTitle} — {localSynthetic}";
		var cloudInfo = $"{cloudChapterTitle} — {cloudSynthetic}";
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
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed updating local book progress in DB: {ex.Message}");
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
		};
		await syncService.SaveProgressAsync(progress, token);
	}

	async Task ApplyProgressToUiAsync(ReadingProgress progress)
	{
		book.CurrentChapter = progress.CurrentChapter;
		book.CurrentPage = progress.CurrentPage;

		if (book.Chapters.Count > book.CurrentChapter && book.CurrentChapter >= 0)
		{
			await webViewHelper.LoadPageAsync(pageLabel, book);
			if (book.CurrentPage > 0)
			{
				await webView.EvaluateJavaScriptAsync($"gotoPage({book.CurrentPage})");
			}
		}

		pageLabel.Text = await GetCurrentPageInfoAsync();
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
				pageLabel.Text = await GetCurrentPageInfoAsync();
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

			pageLabel.Text = await GetCurrentPageInfoAsync();
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
}