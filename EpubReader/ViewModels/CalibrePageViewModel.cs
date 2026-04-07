using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Extensions;
using EpubReader.ODPS;

namespace EpubReader.ViewModels;

public partial class CalibrePageViewModel : BaseViewModel
{
	bool isAlphabeticalSorted = false;

	[ObservableProperty]
	public partial bool Cancelled { get; set; } = false;

	[ObservableProperty]
	public partial ObservableCollection<Book> Books { get; set; }

	[ObservableProperty]
	public partial List<OpdsFeed> OpdsFeed { get; set; } = [];

	[ObservableProperty]
	public partial OpdsFeed Feed { get; set; } = new();
	[ObservableProperty]
	public partial string EmptyLabelText { get; set; } = "No books found in Calibre library.\nPlease load books from your Calibre server.";
	public List<Book> BookList { get; set; } = [];

	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();

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
		if (Books.Count > 0)
		{
			Feed.Entries.Clear();
			Books.Clear();
			Logger.Warn("Books are already loaded, clearing list and continuing.");
		}

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
		
		if (!await ValidateUrl($"{settings.UrlPrefix}://{settings.IPAddress}:{settings.Port}", settings.UrlPrefix))
		{
			return;
		}
		
		Logger.Info("Loading books from Calibre server...");
		await GetFeedList(new FeedReader(), settings.IPAddress, settings.Port, settings.UrlPrefix);
		await LoadCalibreDataFromUrl(settings.IPAddress, settings.Port, settings.UrlPrefix);

	}
	async Task GetFeedList(FeedReader feedReader, string ipAddress, int port, string prefix)
	{
		var url = $"{prefix}://{ipAddress}:{port}/opds";
		var mainFeed = await feedReader.GetFeedAsync(url, CancellationTokenSource.Token);
		var title = mainFeed.Entries.FirstOrDefault(e => e.Title == "By Title")?.Links ?? [];
		var authors = mainFeed.Entries.FirstOrDefault(e => e.Title == "By Authors")?.Links ?? [];
		var newestFeed = mainFeed.Entries.FirstOrDefault(e => e.Title == "By Newest")?.Links ?? [];
		OpdsFeed.Add(new OpdsFeed
		{
			Title = "By Title",
			Links = title,
		});
		OpdsFeed.Add(new OpdsFeed
		{
			Title = "By Authors",
			Links = authors,
		});
		OpdsFeed.Add(new OpdsFeed
		{
			Title = "By Newest",
			Links = newestFeed,
		});
		Logger.Info("Feedlist has been downloaded successfully");
	}

	/// <summary>
	/// Loads book data from a Calibre server feed and populates the book collection.
	/// </summary>
	/// <remarks>This method retrieves the latest feed from the specified Calibre server and processes each entry to
	/// populate the book collection. It logs the number of books found and handles cancellation requests. If no books are
	/// found, a warning is logged and an appropriate message is set.</remarks>
	async Task LoadCalibreDataFromUrl(string ipAddress, int port, string prefix)
	{
		var settings = await db.GetSettings() ?? new Settings();
		var feedReader = new FeedReader();
		var newestFeedUrl = OpdsFeed.FirstOrDefault(f => f.Title == "By Newest")?.Links.FirstOrDefault()?.Href ?? string.Empty;
		Logger.Info($"Newest feed URL: {newestFeedUrl}");
		var uri = new Uri(new Uri($"{prefix}://{ipAddress}:{port}/opds"), newestFeedUrl);
		Feed = await feedReader.GetFeedAsync(uri.AbsoluteUri, CancellationTokenSource.Token);
		if (Feed.Entries.Count == 0)
		{
			Logger.Warn("No books found in the Calibre feed.");
			EmptyLabelText = "No books found in the Calibre feed.";
			return;
		}
		Logger.Info($"Number of books found: {Feed.Entries.Count}");

		using var client = new HttpClient();
		foreach (var entry in Feed.Entries)
		{
			if (entry is null)
			{
				Logger.Warn("Encountered a null entry in the feed. Skipping...");
				continue;
			}
			if (CancellationTokenSource.IsCancellationRequested)
			{
				Cancelled = true;
				Logger.Info("Loading books cancelled by user.");
				break;
			}

#pragma warning disable S5332 // False positive! This is not a security issue. I am filtering a string value that happens to be a URL.
			var imageUrl = entry.Links.FirstOrDefault(l => l.Rel == "http://opds-spec.org/image")?.Href ?? string.Empty;
			var DownloadList = entry.Links.FindAll(l => l.Rel == "http://opds-spec.org/acquisition") ?? [];
			var downloadUrl = DownloadList.FirstOrDefault(l => l.Type == "application/epub+zip")?.Href ?? string.Empty;
			if (string.IsNullOrEmpty(downloadUrl))
			{
				Logger.Warn($"Entry '{entry.Title}' is missing image or download URL. Skipping...");
				continue;
			}
#pragma warning restore S5332 // False positive! This is not a security issue. I am filtering a string value that happens to be a URL.
			var book = new Book
			{
				Title = entry.Title ?? string.Empty,
				Author = entry.Authors.FirstOrDefault()?.Name ?? string.Empty,
				PublishedDate = entry.Published ?? DateTime.MinValue,
				Description = entry.Content ?? string.Empty,
				DownloadUrl = $"{settings.UrlPrefix}://{settings.IPAddress}:{settings.Port}/{downloadUrl}",
				Thumbnail = $"{settings.UrlPrefix}://{settings.IPAddress}:{settings.Port}/{imageUrl}",
				IsInLibrary = await processEpubFiles.IsBookAlreadyInLibrary(new Book { Title = entry.Title ?? string.Empty })
			};
			Books.Add(book);
			BookList.Add(book);
		}
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

			Logger.Warn($"URL address {url} is not local or permitted external. Using default base URL.");
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
			List<(string IpAddress, int Port)> discoveredServers = await CalibreZeroConf.DiscoverCalibreServers().ConfigureAwait(false);
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

	readonly record struct CalibreServerAddress(string UrlPrefix, string IPAddress, int Port);
	readonly record struct CalibreServerResolution(CalibreServerAddress Address, bool UsedSavedEndpoint);
}