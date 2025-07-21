using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.ODPS;
using EpubReader.Util;
using EpubReader.Views;
using FileInfo = EpubReader.Models.FileInfo;
namespace EpubReader.ViewModels;
public partial class CalibrePageViewModel : BaseViewModel
{
	[ObservableProperty]
	public partial string EmptyLabelText { get; set; } = "No books found in Calibre library.\nPlease load books from your Calibre server.";
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	string baseUrl = string.Empty; // Replace with your actual Calibre server URL
	CancellationTokenSource cancellationTokenSource = new();
	readonly bool isLoaded = false;
	Popup settingsPopup = new CalibreSettingsPage(new CalibreSettingsPageViewModel());
	readonly PopupOptions settingsOptions = new()
	{
		CanBeDismissedByTappingOutsideOfPopup = true,
	};

	[ObservableProperty]
	public partial bool Cancelled { get; set; } = false;

	[ObservableProperty]
	public partial ObservableCollection<Book> Books { get; set; }

	public List<Book> BookList { get; set; } = [];

	[ObservableProperty]
	public partial OpdsFeed Feed { get; set; } = new();

	public CalibrePageViewModel()
	{
		Books = [];
		if (isLoaded)
		{
			Logger.Warn("CalibrePageViewModel is already loaded, skipping initialization.");
			return;
		}
		WeakReferenceMessenger.Default.Register<CalibreMessage>(this, (r, m) =>
		{
			if (m.Value)
			{
				Cancelled = true;
				cancellationTokenSource.Cancel();
				Logger.Info("Calibre loading cancelled by user.");
			}
		});
		isLoaded = true;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			cancellationTokenSource?.Dispose();
			processEpubFiles.Dispose();
			WeakReferenceMessenger.Default.UnregisterAll(this);
			Logger.Info("CalibrePageViewModel disposed.");
		}
		base.Dispose(disposing);
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
		if (cancellationTokenSource.IsCancellationRequested)
		{
			cancellationTokenSource = new CancellationTokenSource();
		}
		book.IsInLibrary = await processEpubFiles.ProcessFileAsync(book, baseUrl, cancellationTokenSource.Token).ConfigureAwait(false);
	}

	/// <summary>
	/// Cancels the current operation.
	/// </summary>
	/// <remarks>This method is typically used to abort an ongoing process or task. Ensure that the operation being
	/// canceled supports cancellation and that any necessary cleanup is performed after calling this method.</remarks>
	[RelayCommand]
	public void Cancel()
	{
		cancellationTokenSource.Cancel();
		Cancelled = true;
	}

	/// <summary>
	/// Displays the settings page for configuring application settings.
	/// </summary>
	/// <remarks>Clears the current book list before displaying the settings page to ensure any changes to server
	/// address or other settings are reflected.</remarks>
	[RelayCommand]
	public void Settings()
	{
		// Clear the current book list before showing settings in case they change server address or other settings
		Books.Clear();

		settingsPopup = new CalibreSettingsPage(new CalibreSettingsPageViewModel());
		Shell.Current.ShowPopup(settingsPopup, settingsOptions);
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
			Books.Clear();
			Logger.Warn("Books are already loaded, clearing list and continuing.");
		}
		if (Feed?.Entries.Count > 0)
		{
			Feed.Entries.Clear();
		}

		if (cancellationTokenSource.IsCancellationRequested)
		{
			cancellationTokenSource = new CancellationTokenSource();
			Logger.Info("Cancellation token source reset.");
		}

		try
		{
			WeakReferenceMessenger.Default.Register<BookMessage>(this, (r, m) => OnAddBooks(m.Value));
			Logger.Info("Initializing Url...");
			var settings = await db.GetSettings() ?? new Settings();
			var (ipAddress, port) = (settings.IPAddress, settings.Port);

			if (settings.CalibreAutoDiscovery)
			{
				Logger.Info("Calibre auto discovery is enabled, initializing IP address...");
#if WINDOWS
				(ipAddress, port) = await InitializeIpAddress().ConfigureAwait(true);
#endif	
			}
			else
			{
				Logger.Info("Calibre auto discovery is disabled, using settings from database.");
			}

			var urlPrefix = settings.UrlPrefix;
			var prefix = urlPrefix.ToLowerInvariant() switch
			{
				"http" => "http",
				"https" => "https",
				_ => "http"
			};
			baseUrl = $"{prefix}://{ipAddress}:{port}";
			Logger.Info($"Using IP address: {ipAddress}, Port: {port}, prefix: {prefix}");
			
			if (!await ValidateUrl(baseUrl, prefix))
			{
				Logger.Warn($"URL validation failed for {baseUrl}");
				return;
			}

			Logger.Info("Loading books from Calibre server...");
			await LoadCalibreDataFromUrl(ipAddress, port, prefix);
		}
		catch (Exception ex)
		{
			Logger.Error($"An error occurred while creating the popup dialog: {ex.Message}");
		}
		finally
		{
			WeakReferenceMessenger.Default.UnregisterAll(this);
			Logger.Info("LoadBooks completed successfully.");
		}
	}

	/// <summary>
	/// Constructs and retrieves the URL of the newest feed entry from an OPDS feed.
	/// </summary>
	/// <remarks>This method constructs a base URL using the provided IP address, port, and prefix, then attempts to
	/// retrieve the main feed. It searches for the "By Newest" entry within the feed and returns its URL. If the entry is
	/// not found, an empty string is returned.</remarks>
	/// <param name="feedReader">The <see cref="FeedReader"/> instance used to fetch the feed data.</param>
	/// <param name="ipAddress">The IP address of the server hosting the OPDS feed.</param>
	/// <param name="port">The port number on which the OPDS feed is accessible.</param>
	/// <param name="prefix">The URL scheme prefix, such as "http" or "https".</param>
	/// <returns>A <see cref="string"/> representing the URL of the newest feed entry. Returns an empty string if the "By Newest"
	/// feed link is not found.</returns>
	async Task<string> GetFeedUrl(FeedReader feedReader, string ipAddress, int port, string prefix)
	{
		var url = $"{prefix}://{ipAddress}:{port}/opds";
		var mainFeed = await feedReader.GetFeedAsync(url, cancellationTokenSource.Token);
		Logger.Info($"Main feed loaded from {url}");
		var newestFeedLink = mainFeed.Entries.FirstOrDefault(e => e.Title == "By Newest")?.Links.FirstOrDefault()?.Href;
		Logger.Info($"Newest feed link found: {newestFeedLink}");
		if (string.IsNullOrEmpty(newestFeedLink))
		{
			Logger.Warn("Could not find 'By Newest' feed link.");
			return string.Empty;
		}
		return new Uri(new Uri(url), newestFeedLink).ToString();
	}

	/// <summary>
	/// Loads book data from a Calibre server feed and populates the book collection.
	/// </summary>
	/// <remarks>This method retrieves the latest feed from the specified Calibre server and processes each entry to
	/// populate the book collection. It logs the number of books found and handles cancellation requests. If no books are
	/// found, a warning is logged and an appropriate message is set.</remarks>
	/// <param name="ipAddress">The IP address of the Calibre server.</param>
	/// <param name="port">The port number on which the Calibre server is running.</param>
	/// <param name="prefix">The URL prefix for accessing the Calibre feed.</param>
	/// <returns></returns>
	async Task LoadCalibreDataFromUrl(string ipAddress, int port, string prefix)
	{
		var feedReader = new FeedReader();
		var newestFeedUrl = await GetFeedUrl(feedReader, ipAddress, port, prefix);
		Logger.Info($"Newest feed URL: {newestFeedUrl}");
		Feed = await feedReader.GetFeedAsync(newestFeedUrl, cancellationTokenSource.Token);
		int numberOfBooks = Feed.Entries.Count;
		if (numberOfBooks == 0)
		{
			Logger.Warn("No books found in the Calibre feed.");
			EmptyLabelText = "No books found in the Calibre feed.";
			return;
		}
		Logger.Info($"Number of books found: {numberOfBooks}");
		int count = 0;

		using var client = new HttpClient();
		foreach (var entry in Feed.Entries)
		{
			if (entry is null)
			{
				Logger.Warn("Encountered a null entry in the feed. Skipping...");
				continue;
			}
			if (cancellationTokenSource.IsCancellationRequested)
			{
				Cancelled = true;
				Logger.Info("Loading books cancelled by user.");
				break;
			}
			var folderinfo = new FileInfo
			{
				Count = count,
				MaxCount = numberOfBooks,
				Title = entry.Title ?? string.Empty,
			};
#pragma warning disable S5332 // False positive! This is not a security issue. I am filtering a string value that happens to be a URL.
			var imageUrl = entry.Links.FirstOrDefault(l => l.Rel == "http://opds-spec.org/image")?.Href ?? string.Empty;
#pragma warning restore S5332 // False positive! This is not a security issue. I am filtering a string value that happens to be a URL.
			var book = new Book
			{
				Title = entry.Title ?? string.Empty,
				Author = entry.Authors.FirstOrDefault()?.Name ?? string.Empty,
				Date = entry.Updated?.ToString("yyyy-MM-dd") ?? string.Empty,
				Description = entry.Content ?? string.Empty,
#pragma warning disable S5332 // False positive! This is not a security issue. I am filtering a string value that happens to be a URL.
				DownloadUrl = entry.Links.FirstOrDefault(l => l.Rel == "http://opds-spec.org/acquisition")?.Href ?? string.Empty,
#pragma warning restore S5332 // False positive! This is not a security issue. I am filtering a string value that happens to be a URL.
				Thumbnail = baseUrl + "/" + imageUrl,
				IsInLibrary = await processEpubFiles.IsBookAlreadyInLibrary(new Book { Title = entry.Title ?? string.Empty })
			};

#if WINDOWS || MACCATALYST
			if (count % 100 == 0)
			{
				WeakReferenceMessenger.Default.Send(new FileMessage(folderinfo));
			}
#endif
#if ANDROID || IOS
			if (count % 5 == 0)
			{
				WeakReferenceMessenger.Default.Send(new FileMessage(folderinfo));
			}
#endif
			Books.Add(book);
			count++;
		}
	}

	/// <summary>
	/// Adds a book to the library if it does not already exist.
	/// </summary>
	/// <remarks>If the book already exists in the library, it will not be added again, and an informational log
	/// entry will be created. If the book is <see langword="null"/>, a warning log entry will be created.</remarks>
	/// <param name="book">The book to be added. Must not be <see langword="null"/>.</param>
	void OnAddBooks(Book book)
	{
		if (book is not null)
		{
			if (Books.Any(b => b.Title == book.Title))
			{
				Logger.Info($"Book already exists in library: {book.Title}");
				return;
			}

			book.IsInLibrary = true; // Mark the book as in library
			Books.Add(book);
			Logger.Info($"Book message received: {book.Title}");
		}
		else
		{
			Logger.Warn("Received null book message");
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


#if WINDOWS
	/// <summary>
	/// Initializes the base URL by discovering available Calibre servers on the network.
	/// </summary>
	/// <remarks>This method attempts to find Calibre servers using a network discovery process. If servers are
	/// found, it sets the base URL to the first discovered server's address. If no servers are found, a default base URL
	/// is used.</remarks>
	/// <returns>A task that represents the asynchronous operation.</returns>
	async Task<(string IPAddress, int Port)> InitializeIpAddress()
	{
		var db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException("Database service is not available.");
		var settings = await db.GetSettings() ?? new Settings();
		var urlPrefix = settings.UrlPrefix;
		baseUrl = $"{urlPrefix}://{settings.IPAddress}:{settings.Port}";
		
		List<(string IpAddress, int Port)> servers = [];
		if(settings.CalibreAutoDiscovery)
		{
			servers = await CalibreZeroConf.DiscoverCalibreServers().ConfigureAwait(false);
			baseUrl = $"{urlPrefix}://{servers[0].IpAddress}:{servers[0].Port}";
		}
		
		if (servers.Count > 1)
		{
			Logger.Info($"Using discovered Calibre server at {baseUrl}");
		}
		else
		{
			servers.Add((settings.IPAddress, settings.Port));
			baseUrl = $"{urlPrefix}://{settings.IPAddress}:{settings.Port}";
			Logger.Warn("No Calibre servers found. Using default base URL.");
		}

		return (servers[0].IpAddress, servers[0].Port);
	}
#endif
}