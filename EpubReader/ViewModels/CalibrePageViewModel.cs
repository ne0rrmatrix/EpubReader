using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Extensions;
using EpubReader.ODPS;

namespace EpubReader.ViewModels;

public partial class CalibrePageViewModel : BaseViewModel
{
	bool isAlphabeticalSorted = false;
	string calibreServerBaseUrl = string.Empty;
	string searchUrlTemplate = string.Empty;
	string currentFeedUrl = string.Empty;
	string currentFeedEmptyLabelText = "No books found in Calibre library.\nPlease load books from your Calibre server.";
	CancellationTokenSource searchCancellationTokenSource = new();
	readonly Stack<CalibreFeedNavigationState> feedNavigationStack = [];
	public List<Book> BookList { get; set; } = [];
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	
	[ObservableProperty]
	public partial bool Cancelled { get; set; } = false;

	[ObservableProperty]
	public partial ObservableCollection<Book> Books { get; set; }

	[ObservableProperty]
	public partial ObservableCollection<OpdsFeedSelection> FeedSelections { get; set; } = [];

	[ObservableProperty]
	public partial OpdsFeedSelection? SelectedFeedSelection { get; set; } = new();

	[ObservableProperty]
    public partial OpdsFeed Feed { get; set; } = new();

	[ObservableProperty]
    public partial string CurrentFeedTitle { get; set; } = "Browse feeds";

	[ObservableProperty]
	public partial bool CanNavigateBack { get; set; }

	[ObservableProperty]	
	public partial string SearchText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string EmptyLabelText { get; set; } = "No books found in Calibre library.\nPlease load books from your Calibre server.";

	public CalibrePageViewModel()
	{
		Books = [];
	}

	/// <summary>
	/// Adds a book to the collection of books.
	/// </summary>
	/// <remarks>This method adds the specified <paramref name="book"/> to the <see cref="Book"/>
	/// collection.</remarks>
	/// <param name="book">The book to add to the collection. Cannot be null.</param>
	/// <returns></returns>
	[RelayCommand]
	public async Task AddBook(Book book)
	{
		if (CancellationTokenSource.IsCancellationRequested)
		{
			CancellationTokenSource = new CancellationTokenSource();
		}
		book.IsInLibrary = await processEpubFiles.ProcessFileAsync(book, CancellationTokenSource.Token);
		if (book.IsInLibrary)
		{
			await ShowInfoToastAsync($"Book '{book.Title}' has been added to your library.");
		}
		else
		{
			await ShowInfoToastAsync($"Book '{book.Title}' could not be added to your library.");
		}
	}

	/// <summary>
	/// Cancels the current operation.
	/// </summary>
	/// <remarks>This method is typically used to abort an ongoing process or task. Ensure that the operation being
	/// canceled supports cancellation and that any necessary cleanup is performed after calling this method.</remarks>
	[RelayCommand]
	public void Cancel()
	{
		CancellationTokenSource.Cancel();
		Cancelled = true;
	}

	/// <summary>
	/// Displays the settings page for configuring application settings.
	/// </summary>
	/// <remarks>Clears the current book list before displaying the settings page to ensure any changes to server
	/// address or other settings are reflected.</remarks>
	[RelayCommand]
	public async Task Settings(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		var popup = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<CalibreSettingsPage>() ?? throw new InvalidOperationException("Calibre settings popup is not available.");
		PopupOptions options = new()
		{
			CanBeDismissedByTappingOutsideOfPopup = false,
		};

		var result = await Shell.Current.ShowPopupAsync<bool>(popup, options, token);
		if (result.Result)
		{
			EmptyLabelText = "Calibre settings verified. Tap refresh to load books from the saved server.";
			await ShowInfoToastAsync("Calibre settings verified and saved.");
		}
	}

	/// <summary>
	/// Toggles the sorting order of the book collection by author name.
	/// </summary>
	/// <remarks>When called, this method switches between sorting the books in ascending (A-Z) and descending (Z-A)
	/// order based on the author's name. The current sorting order is logged for reference.</remarks>
	[RelayCommand]
	public void AlphabeticalAuthorSort()
	{
		isAlphabeticalSorted = !isAlphabeticalSorted;
		Books = [.. SortByAuthor([.. Books], isAlphabeticalSorted)];
	}


	/// <summary>
	/// Sorts the collection of books in alphabetical order by their titles.
	/// </summary>
	/// <remarks>This method sorts the books in a case-insensitive manner using the ordinal string comparison. After
	/// sorting, the original collection is cleared and repopulated with the sorted books.</remarks>
	[RelayCommand]
	public void AlphabeticalTitleSort()
	{
		isAlphabeticalSorted = !isAlphabeticalSorted;
		Books = [.. SortByTitle([.. Books], isAlphabeticalSorted)];
	}

	/// <summary>
	/// Sorts a list of books by their titles in either ascending or descending order.
	/// </summary>
	[RelayCommand]
	public void SortByDate()
	{
		isAlphabeticalSorted = !isAlphabeticalSorted;
		if (isAlphabeticalSorted)
		{
			Logger.Info("Sorting books by date (Most Recent - Oldest)");
			Books = [.. Books.OrderBy(b => b.PublishedDate)];
			return;
		}
		Logger.Info("Sorting books by date (Oldest - Most Recent)");
		Books = [.. Books.OrderByDescending(b => b.PublishedDate)];
	}

	public async Task SelectFeedAsync(OpdsFeedSelection? selection, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		if (selection is null || string.IsNullOrWhiteSpace(selection.FeedUrl))
		{
			return;
		}

		CancelSearchRequests();
		if (!string.IsNullOrWhiteSpace(SearchText))
		{
			SearchText = string.Empty;
		}

		using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, CancellationTokenSource.Token);
		var feedReader = new FeedReader();
		OpdsFeed? feed = new();
		try
		{
			string feedUrl = NormalizeOpdsHref(selection.FeedUrl);
			feed = await feedReader.GetFeedAsync(feedUrl, linkedTokenSource.Token);
		}
		catch (HttpRequestException ex)
		{
			Logger.Error($"Failed to load feed from {selection.FeedUrl}: {ex.Message}");
			await ShowInfoToastAsync("Failed to load the selected feed. Please check your network connection and try again.");
			return;
		}
		catch (OperationCanceledException)
		{
			Logger.Info("Feed selection cancelled by user.");
			return;
		}
		

		if (IsNavigationFeed(feed))
		{
			feedNavigationStack.Push(CreateNavigationStateSnapshot());
			UpdateNavigationState();
			ApplyFeedSelections(feed, selection.FeedUrl);
			CurrentFeedTitle = selection.Title;
			currentFeedUrl = selection.FeedUrl;
			Feed = feed;
			BookList = [];
			Books = [];
			currentFeedEmptyLabelText = $"Select an entry from {selection.Title}.";
			EmptyLabelText = currentFeedEmptyLabelText;
			Logger.Info($"Loaded navigation feed '{selection.Title}' with {FeedSelections.Count} entries.");
		}
		else
		{
			await LoadBookFeedAsync(feed, selection.FeedUrl, selection.Title, linkedTokenSource.Token);
		}

		SelectedFeedSelection = null;
	}

	public async Task SearchBooksAsync(string? searchText, CancellationToken token = default)
	{
		SearchText = searchText?.Trim() ?? string.Empty;
		ResetSearchCancellationTokenSource(token);
		var searchToken = searchCancellationTokenSource.Token;

		if (string.IsNullOrWhiteSpace(SearchText))
		{
			RestoreCurrentBookResults();
			return;
		}

		try
		{
			await Task.Delay(300, searchToken);

			if (string.IsNullOrWhiteSpace(searchUrlTemplate))
			{
				ApplyLocalSearch(SearchText);
				return;
			}

			var searchUrl = BuildSearchUrl(SearchText);
			if (string.IsNullOrWhiteSpace(searchUrl))
			{
				ApplyLocalSearch(SearchText);
				return;
			}

			var searchFeed = await new FeedReader().GetFeedAsync(searchUrl, searchToken);
			var searchResults = await BuildBooksFromFeedAsync(searchFeed, searchToken);
			Books = [.. searchResults];
			EmptyLabelText = $"No books found matching '{SearchText}'.";
			Logger.Info($"Loaded {searchResults.Count} search results for '{SearchText}'.");
		}
		catch (OperationCanceledException)
		{
			Logger.Info("Calibre search request cancelled.");
		}
	}

	[RelayCommand]
	public void NavigateBack()
	{
		CancelSearchRequests();
		SearchText = string.Empty;

		if (feedNavigationStack.Count == 0)
		{
			return;
		}

		RestoreNavigationState(feedNavigationStack.Pop());
		UpdateNavigationState();
	}
	/// <summary>
	/// Asynchronously loads books from a Calibre server if they are not already loaded.
	/// </summary>
	/// <remarks>This method registers for book messages and initializes the necessary components to load books. It
	/// logs the process and handles any exceptions that occur during the loading operation. If books are already loaded,
	/// the method logs a warning and exits early.</remarks>
	/// <returns></returns>
	[RelayCommand]
	public async Task LoadBooks()
	{
       ResetFeedState();

		if (CancellationTokenSource.IsCancellationRequested)
		{
			CancellationTokenSource = new CancellationTokenSource();
			Logger.Info("Cancellation token source reset.");
		}

		Logger.Info("Initializing Url...");
		var settings = await db.GetSettings() ?? new Settings();

		if (settings.CalibreAutoDiscovery)
		{
			Logger.Info("Calibre auto discovery is enabled, initializing IP address...");
			EmptyLabelText = HasSavedEndpoint(settings)
				? "Connecting to Calibre server...\nPlease wait while the saved server is being checked."
				: "Connecting to Calibre server...\nPlease wait while the server is being discovered.";

			CalibreServerResolution endpointResolution = await ResolveServerAddressAsync(settings, CancellationTokenSource.Token).ConfigureAwait(true);
			settings.UrlPrefix = endpointResolution.Address.UrlPrefix;
			settings.IPAddress = endpointResolution.Address.IPAddress;
			settings.Port = endpointResolution.Address.Port;
			if (settings.IPAddress == string.Empty || settings.Port == 0)
			{
				Logger.Warn("No Calibre servers found using Bonjour. Please check your network connection.");
				EmptyLabelText = "No Calibre servers found on the network using Bonjour.\nPlease check your network connection.";
				return;
			}
			EmptyLabelText = endpointResolution.UsedSavedEndpoint
				? "Connecting to Calibre server...\nPlease wait while the saved server is being loaded."
				: "Connecting to Calibre server...\nPlease wait while the discovered server is being loaded.";
			settings.CalibreManualUrlPrefix = settings.UrlPrefix;
			settings.CalibreManualIPAddress = settings.IPAddress;
			settings.CalibreManualPort = settings.Port;
			await db.SaveSettings(settings).ConfigureAwait(false);
		}
		else
		{
			if (!TryGetManualSettings(settings, out CalibreServerAddress manualSettings))
			{
				Logger.Warn("Calibre manual settings are incomplete. Prompting the user to update settings.");
				EmptyLabelText = "Enter and verify a manual Calibre server address in Settings before loading books.";
				return;
			}

			settings.UrlPrefix = manualSettings.UrlPrefix;
			settings.IPAddress = manualSettings.IPAddress;
			settings.Port = manualSettings.Port;
			await db.SaveSettings(settings).ConfigureAwait(false);
			Logger.Info("Calibre auto discovery is disabled, using verified manual settings from the database.");
			EmptyLabelText = "Connecting to Calibre server...\nPlease wait while the server is being loaded.";
		}

		Logger.Info($"Using IP address: {settings.IPAddress}, Port: {settings.Port}, prefix: {settings.UrlPrefix}");
		calibreServerBaseUrl = $"{settings.UrlPrefix}://{settings.IPAddress}:{settings.Port}";
		
      if (!await ValidateUrl(calibreServerBaseUrl, settings.UrlPrefix))
		{
			return;
		}
		
		Logger.Info("Loading books from Calibre server...");
      var rootFeedUrl = $"{calibreServerBaseUrl}/opds";
		var rootFeed = await new FeedReader().GetFeedAsync(rootFeedUrl, CancellationTokenSource.Token);
		ApplyFeedSelections(rootFeed, rootFeedUrl);
		CurrentFeedTitle = rootFeed.Title ?? "Library";
		currentFeedUrl = rootFeedUrl;
		Feed = rootFeed;
		currentFeedEmptyLabelText = "Select a Calibre feed to browse books.";
		EmptyLabelText = currentFeedEmptyLabelText;

		var defaultFeedSelection = FeedSelections.FirstOrDefault(selection => string.Equals(selection.Title, "By Newest", StringComparison.OrdinalIgnoreCase))
			?? FeedSelections.FirstOrDefault();
		if (defaultFeedSelection is not null)
		{
			await SelectFeedAsync(defaultFeedSelection, CancellationTokenSource.Token);
			return;
		}

		Logger.Warn("No Calibre feed entries were returned by the OPDS root feed.");

	}

	/// <summary>
	/// Loads book data from a Calibre server feed and populates the book collection.
	/// </summary>
	/// <remarks>This method retrieves the latest feed from the specified Calibre server and processes each entry to
	/// populate the book collection. It logs the number of books found and handles cancellation requests. If no books are
	/// found, a warning is logged and an appropriate message is set.</remarks>
 async Task LoadBookFeedAsync(OpdsFeed feed, string feedUrl, string? feedTitle, CancellationToken token)
	{
        Feed = feed;
		currentFeedUrl = feedUrl;
		CurrentFeedTitle = feedTitle ?? feed.Title ?? "Calibre";
		var books = await BuildBooksFromFeedAsync(feed, token);
		BookList = books;
		Books = [.. books];
		currentFeedEmptyLabelText = $"No books found in {CurrentFeedTitle}.";
		EmptyLabelText = currentFeedEmptyLabelText;

		if (books.Count == 0)
		{
          Logger.Warn($"No books found in the Calibre feed '{CurrentFeedTitle}'.");
			return;
		}

		Logger.Info($"Number of books found in '{CurrentFeedTitle}': {books.Count}");
	}

    async Task<List<Book>> BuildBooksFromFeedAsync(OpdsFeed feed, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		List<Book> books = [];
		foreach (var entry in feed.Entries)
		{
          if (entry is null)
			{
				Logger.Warn("Encountered a null entry in the feed. Skipping...");
				continue;
			}
           token.ThrowIfCancellationRequested();

			if (CancellationTokenSource.IsCancellationRequested)
			{
				Cancelled = true;
				Logger.Info("Loading books cancelled by user.");
				break;
			}

#pragma warning disable S5332 // False positive! This is not a security issue. I am filtering a string value that happens to be a URL.
			byte[] imageName = [];
			var imageUrl = entry.Links.FirstOrDefault(l => l.Rel == "http://opds-spec.org/image")?.Href ?? string.Empty;
			var DownloadList = entry.Links.FindAll(l => l.Rel == "http://opds-spec.org/acquisition") ?? [];
			var downloadUrl = DownloadList.FirstOrDefault(l => l.Type == "application/epub+zip")?.Href ?? string.Empty;
			if (string.IsNullOrEmpty(downloadUrl))
			{
				Logger.Warn($"Entry '{entry.Title}' is missing download URL. Skipping...");
				continue;
			}
			if (string.IsNullOrEmpty(imageUrl))
			{
              Logger.Warn($"Entry '{entry.Title}' is missing image URL. Generating a cover image.");
				if(entry.Title is null)
				{
					Logger.Warn("Entry title is null. Cannot generate cover image without a title. Skipping image generation.");
					continue;
				}
				imageName = EbookService.GenerateCoverImage(entry.Title);
			}

#pragma warning restore S5332 // False positive! This is not a security issue. I am filtering a string value that happens to be a URL.
			var book = new Book
			{
				Title = entry.Title ?? string.Empty,
				Author = entry.Authors.FirstOrDefault()?.Name ?? string.Empty,
                PublishedDate = entry.Published ?? entry.DcDate ?? DateTime.MinValue,
				Description = entry.Content ?? entry.Summary ?? string.Empty,
				DownloadUrl = CombineUrl(calibreServerBaseUrl, downloadUrl),
				Thumbnail = string.IsNullOrWhiteSpace(imageUrl) ? string.Empty : CombineUrl(calibreServerBaseUrl, imageUrl),
				Categories = [.. entry.Categories],
				IsInLibrary = await processEpubFiles.IsBookAlreadyInLibrary(new Book { Title = entry.Title ?? string.Empty })
			};
			if (string.IsNullOrEmpty(imageUrl) && imageName.Length > 0)
			{
				Logger.Info($"Generated cover image for '{entry.Title}' because the entry is missing an image URL.");
                book.CoverImage = imageName;
			}
            books.Add(book);
		}

		return books;
	}

	void ApplyFeedSelections(OpdsFeed feed, string sourceFeedUrl)
	{
		FeedSelections = [.. CalibrePageViewModel.CreateFeedSelections(feed, sourceFeedUrl)];
		Feed = feed;
		var searchLink = feed.Links.FirstOrDefault(link => string.Equals(link.Rel, "search", StringComparison.OrdinalIgnoreCase));
		if (!string.IsNullOrWhiteSpace(searchLink?.Href))
		{
			searchUrlTemplate = CombineUrl(sourceFeedUrl, NormalizeOpdsHref(searchLink.Href));
		}
	}

	static List<OpdsFeedSelection> CreateFeedSelections(OpdsFeed feed, string sourceFeedUrl)
		=> [.. feed.Entries
			.Select(entry => CalibrePageViewModel.CreateFeedSelection(entry, sourceFeedUrl))
			.OfType<OpdsFeedSelection>()];

	static OpdsFeedSelection? CreateFeedSelection(OpdsEntry entry, string sourceFeedUrl)
	{
		var link = entry.Links.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.Href));
		if (link is null)
		{
			return null;
		}

		return new OpdsFeedSelection
		{
			Title = entry.Title ?? string.Empty,
			Description = entry.Content ?? entry.Summary ?? string.Empty,
			FeedUrl = CombineUrl(sourceFeedUrl, NormalizeOpdsHref(link.Href ?? string.Empty)),
			Id = entry.Id ?? string.Empty,
			FeedType = DetermineFeedType(entry),
		};
	}

	void ApplyLocalSearch(string searchText)
	{
		var filteredBooks = BookList.Where(book =>
			book.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
			book.Author.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
			book.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
			book.Language.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
			book.Series.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
			book.Categories.Any(category => category.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
			.ToList();

		Books = [.. filteredBooks];
		EmptyLabelText = $"No books found matching '{searchText}'.";
		Logger.Info($"Applied local Calibre search for '{searchText}' with {filteredBooks.Count} results.");
	}

	void RestoreCurrentBookResults()
	{
		Books = [.. BookList];
		EmptyLabelText = currentFeedEmptyLabelText;
	}

	void ResetFeedState()
	{
		CancelSearchRequests();
		feedNavigationStack.Clear();
		UpdateNavigationState();
		FeedSelections = [];
		SelectedFeedSelection = null;
		Feed = new();
		Books = [];
		BookList = [];
		CurrentFeedTitle = "Browse feeds";
		SearchText = string.Empty;
		calibreServerBaseUrl = string.Empty;
		searchUrlTemplate = string.Empty;
		currentFeedUrl = string.Empty;
		currentFeedEmptyLabelText = "No books found in Calibre library.\nPlease load books from your Calibre server.";
		EmptyLabelText = currentFeedEmptyLabelText;
		Cancelled = false;
	}

	void ResetSearchCancellationTokenSource(CancellationToken token)
	{
		CancelSearchRequests();
		searchCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
	}

	void CancelSearchRequests()
	{
		if (!searchCancellationTokenSource.IsCancellationRequested)
		{
			searchCancellationTokenSource.Cancel();
		}

		searchCancellationTokenSource.Dispose();
		searchCancellationTokenSource = new();
	}

	void RestoreNavigationState(CalibreFeedNavigationState state)
	{
		CurrentFeedTitle = state.CurrentFeedTitle;
		currentFeedUrl = state.CurrentFeedUrl;
		currentFeedEmptyLabelText = state.EmptyLabelText;
		searchUrlTemplate = state.SearchUrlTemplate;
		FeedSelections = [.. state.FeedSelections];
		BookList = [.. state.BookList];
		Books = [.. state.VisibleBooks];
		EmptyLabelText = currentFeedEmptyLabelText;
		SelectedFeedSelection = null;
	}

	CalibreFeedNavigationState CreateNavigationStateSnapshot()
		=> new(
			CurrentFeedTitle,
			currentFeedUrl,
			currentFeedEmptyLabelText,
			searchUrlTemplate,
			[.. FeedSelections],
			[.. BookList],
			[.. Books]);

	void UpdateNavigationState()
		=> CanNavigateBack = feedNavigationStack.Count > 0;

	string BuildSearchUrl(string searchText)
		=> searchUrlTemplate.Replace("{searchTerms}", Uri.EscapeDataString(searchText), StringComparison.Ordinal);

	static bool IsNavigationFeed(OpdsFeed feed)
		=> feed.Entries.Count > 0 && feed.Entries.All(entry => !HasAcquisitionLink(entry));

	static bool HasAcquisitionLink(OpdsEntry entry)
		=> entry.Links.Any(link => link.Rel?.Contains("acquisition", StringComparison.OrdinalIgnoreCase) == true);

	static string DetermineFeedType(OpdsEntry entry)
	{
		if (entry.Id?.StartsWith("calibre-library:", StringComparison.OrdinalIgnoreCase) == true)
		{
			return "library";
		}

		return HasAcquisitionLink(entry) ? "acquisition" : "navigation";
	}

	static string CombineUrl(string baseUrl, string relativeOrAbsoluteUrl)
	{
		if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl))
		{
			return string.Empty;
		}

		if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
		{
			return relativeOrAbsoluteUrl;
		}

		if (Uri.TryCreate(relativeOrAbsoluteUrl, UriKind.Absolute, out var absoluteUri))
		{
			if (absoluteUri.Scheme is "http" or "https")
			{
				return absoluteUri.AbsoluteUri;
			}

			// Calibre/OPDS navigation links must not stay as file://
			if (absoluteUri.Scheme == Uri.UriSchemeFile)
			{
				var pathAndQuery = absoluteUri.PathAndQuery;
				return $"{baseUri.Scheme}://{baseUri.Authority}{pathAndQuery}";
			}

			return relativeOrAbsoluteUrl;
		}

		if (relativeOrAbsoluteUrl.StartsWith('/'))
		{
			return $"{baseUri.Scheme}://{baseUri.Authority}{relativeOrAbsoluteUrl}";
		}

		return new Uri(baseUri, relativeOrAbsoluteUrl).AbsoluteUri;
   }

	/// <summary>
	/// Validates the specified URL to ensure it is either local or a permitted external address, and optionally checks the
	/// SSL certificate if the URL uses HTTPS.
	/// </summary>
	/// <remarks>This method checks if the URL is local or a permitted external address. If the URL is not local or
	/// permitted, it logs a warning and returns <see langword="false"/>. If the URL uses HTTPS, it also validates the SSL
	/// certificate. If the certificate validation fails, it logs an error and returns <see langword="false"/>.</remarks>
	/// <param name="url">The URL to validate. This should be a complete URL string.</param>
	/// <param name="prefix">The expected protocol prefix, such as "http" or "https".</param>
	/// <returns><see langword="true"/> if the URL is valid and accessible; otherwise, <see langword="false"/>.</returns>
	async Task<bool> ValidateUrl(string url, string prefix)
	{
		if (NetworkChecker.IsAddressLocalOrPermittedExternal(url))
		{
			Logger.Info($"Using local or permitted external URL address: {url}");
		}
		else
		{
			// If the URL is not local and/or does not use https, log a warning and use localhost as a fallback

			Logger.Warn($"URL address {url} is not local or permitted. Using default base URL.");
			EmptyLabelText = "Web Address must be local\nif using http: Please upgrade to https\nin order to access a\ncalibre server on the\ninternet!";
			return false;
		}

		if (!await NetworkChecker.ValidateNetworkConnection(url))
		{
			Logger.Warn($"Network connection failed for {url}");
			EmptyLabelText = "Network connection failed. Please check your settings.";
			return false;
		}


		if (prefix.Equals("https") && !await NetworkChecker.ValidateSSLCertificate(url))
		{
			Logger.Error($"SSL certificate validation failed for {url}");
			EmptyLabelText = "SSL certificate validation failed.\nPlease check your Calibre server settings.";
			return false;
		}
		Logger.Info($"URL {url} is valid and accessible.");
		return true;
	}

	/// <summary>
	/// Initializes the base URL by discovering available Calibre servers on the network.
	/// </summary>
	/// <remarks>This method attempts to find Calibre servers using a network discovery process. If servers are
	/// found, it sets the base URL to the first discovered server's address. If no servers are found, a default base URL
	/// is used.</remarks>
	/// <returns>A task that represents the asynchronous operation.</returns>
	async Task<CalibreServerResolution> ResolveServerAddressAsync(Settings settings, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		if (await TryGetReachableSavedEndpointAsync(settings, token).ConfigureAwait(false) is CalibreServerAddress cachedEndpoint)
		{
			Logger.Info($"Using previously verified Calibre endpoint at {cachedEndpoint.UrlPrefix}://{cachedEndpoint.IPAddress}:{cachedEndpoint.Port} before network discovery.");
			return new CalibreServerResolution(cachedEndpoint, true);
		}

		if (settings.CalibreAutoDiscovery)
		{
			List<(string IpAddress, int Port)> discoveredServers = await CalibreZeroConf.DiscoverCalibreServers(cancellationToken: token).ConfigureAwait(false);
			if (discoveredServers.Count == 0)
			{
				return new CalibreServerResolution(new CalibreServerAddress(string.Empty, string.Empty, 0), false);
			}

			CalibreServerAddress discoveredServer = new(settings.UrlPrefix, discoveredServers[0].IpAddress, discoveredServers[0].Port);
			Logger.Info($"Using discovered Calibre server at {discoveredServer.UrlPrefix}://{discoveredServer.IPAddress}:{discoveredServer.Port}");
			return new CalibreServerResolution(discoveredServer, false);
		}

		Logger.Warn("No Calibre servers found. Using default base URL.");
		return new CalibreServerResolution(new CalibreServerAddress(settings.UrlPrefix, settings.IPAddress, settings.Port), false);
	}

	static bool HasSavedEndpoint(Settings settings)
		=> GetSavedEndpoints(settings).Any();

	static async Task<CalibreServerAddress?> TryGetReachableSavedEndpointAsync(Settings settings, CancellationToken token)
	{
		foreach (var endpoint in GetSavedEndpoints(settings))
		{
			token.ThrowIfCancellationRequested();

			string url = $"{endpoint.UrlPrefix}://{endpoint.IPAddress}:{endpoint.Port}/opds";
			if (await NetworkChecker.ValidateNetworkConnection(url, token).ConfigureAwait(false))
			{
				return endpoint;
			}
		}

		return null;
	}

	static IEnumerable<CalibreServerAddress> GetSavedEndpoints(Settings settings)
	{
		HashSet<string> seenEndpoints = new(StringComparer.OrdinalIgnoreCase);
		foreach (var endpoint in new[]
		{
			TryCreateSavedEndpoint(settings.UrlPrefix, settings.IPAddress, settings.Port),
			TryCreateSavedEndpoint(settings.CalibreManualUrlPrefix, settings.CalibreManualIPAddress, settings.CalibreManualPort),
		})
		{
			if (endpoint is not CalibreServerAddress candidate)
			{
				continue;
			}

			string key = $"{candidate.UrlPrefix}://{candidate.IPAddress}:{candidate.Port}";
			if (seenEndpoints.Add(key))
			{
				yield return candidate;
			}
		}
	}

	static CalibreServerAddress? TryCreateSavedEndpoint(string prefix, string host, int port)
	{
		if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(host) || port <= 0)
		{
			return null;
		}

		return new CalibreServerAddress(prefix, host, port);
	}

	static bool TryGetManualSettings(Settings settings, out CalibreServerAddress manualSettings)
	{
		if (string.IsNullOrWhiteSpace(settings.CalibreManualIPAddress) || string.IsNullOrWhiteSpace(settings.CalibreManualUrlPrefix) || settings.CalibreManualPort <= 0)
		{
			manualSettings = default;
			return false;
		}

		manualSettings = new CalibreServerAddress(settings.CalibreManualUrlPrefix, settings.CalibreManualIPAddress, settings.CalibreManualPort);
		return true;
	}

  readonly record struct CalibreFeedNavigationState(
		string CurrentFeedTitle,
		string CurrentFeedUrl,
		string EmptyLabelText,
		string SearchUrlTemplate,
		List<OpdsFeedSelection> FeedSelections,
		List<Book> BookList,
		List<Book> VisibleBooks);

	readonly record struct CalibreServerAddress(string UrlPrefix, string IPAddress, int Port);
	readonly record struct CalibreServerResolution(CalibreServerAddress Address, bool UsedSavedEndpoint);

	static string NormalizeOpdsHref(string href)
	{
		if (string.IsNullOrWhiteSpace(href))
		{
			return string.Empty;
		}

		int encodedQueryIndex = href.IndexOf("%3F", StringComparison.OrdinalIgnoreCase);
		if (encodedQueryIndex >= 0)
		{
			href = string.Concat(
				href.AsSpan(0, encodedQueryIndex),
				"?",
				href.AsSpan(encodedQueryIndex + 3));
		}

		int queryIndex = href.IndexOf('?');
		if (queryIndex >= 0)
		{
			string path = href[..(queryIndex + 1)];
			string query = href[(queryIndex + 1)..].Replace("%26", "&", StringComparison.OrdinalIgnoreCase);
			return path + query;
		}

		return href;
	}
}