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
using EpubReader.Util;
using EpubReader.Views;
using MetroLog;
using FileInfo = EpubReader.Models.FileInfo;
namespace EpubReader.ViewModels;
public partial class CalibrePageViewModel : BaseViewModel
{
	[ObservableProperty]
	public partial string EmptyLabelText { get; set; } = "No books found in Calibre library.\nPlease load books from your Calibre server.";
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(CalibrePageViewModel));
	static string baseUrl = string.Empty; // Replace with your actual Calibre server URL
	readonly CalibreScraper calibreScraper = new();
	CancellationTokenSource cancellationTokenSource = new();
	readonly bool isLoaded = false;
	Popup settingsPopup = new CalibreSettingsPage(new CalibreSettingsPageViewModel());
	Popup popup = new FileDialogePage(new FileDialogePageViewModel());
	readonly PopupOptions options = new()
	{
		CanBeDismissedByTappingOutsideOfPopup = false,
	};
	readonly PopupOptions settingsOptions = new()
	{
		CanBeDismissedByTappingOutsideOfPopup = true,
	};

	[ObservableProperty]
	public partial bool Cancelled { get; set; } = false;
	
	[ObservableProperty]
	public partial ObservableCollection<Book> Books { get; set; }
	
	public CalibrePageViewModel()
	{
		Books = [];
		if(isLoaded)
		{
			logger.Warn("CalibrePageViewModel is already loaded, skipping initialization.");
			return;
		}
		WeakReferenceMessenger.Default.Register<CalibreMessage>(this, (r, m) =>
		{
			if (m.Value)
			{
				Cancelled = true;
				cancellationTokenSource.Cancel();
				logger.Info("Calibre loading cancelled by user.");
			}
		});
		isLoaded = true;
	}

	protected override void Dispose(bool disposing)
	{
		if(disposing)
		{
			// Dispose of any resources that are disposable
			cancellationTokenSource?.Dispose();
			WeakReferenceMessenger.Default.UnregisterAll(this);
			logger.Info("CalibrePageViewModel disposed.");
		}
		base.Dispose(disposing);
	}
	
	/// <summary>
	/// Initializes the base URL by discovering available Calibre servers on the network.
	/// </summary>
	/// <remarks>This method attempts to find Calibre servers using a network discovery process. If servers are
	/// found, it sets the base URL to the first discovered server's address. If no servers are found, a default base URL
	/// is used.</remarks>
	/// <returns>A task that represents the asynchronous operation.</returns>
	static async Task<(string IPAddress, int Port)> InitializeIpAddress()
	{
		var db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException("Database service is not available.");
		var settings = await db.GetSettings() ?? new Settings();
		var urlPrefix = settings.UrlPrefix;
		baseUrl = $"{urlPrefix}://{settings.IPAddress}:{settings.Port}";
		List<(string IpAddress, int Port)> servers = [];
		if (settings.CalibreAutoDiscovery)
		{
			servers = await CalibreZeroConf.DiscoverCalibreServers().ConfigureAwait(false);
			baseUrl = $"{urlPrefix}://{servers[0].IpAddress}:{servers[0].Port}";
		}

		if (servers.Count > 0)
		{
			logger.Info($"Using discovered Calibre server at {baseUrl}");
		}
		else
		{
			servers.Clear();
			servers.Add((settings.IPAddress, settings.Port));
			baseUrl = $"{urlPrefix}://{settings.IPAddress}:{settings.Port}";
			logger.Warn("No Calibre servers found. Using default base URL.");
		}

		return (servers[0].IpAddress, servers[0].Port);
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
		if(cancellationTokenSource.IsCancellationRequested)
		{
			cancellationTokenSource = new CancellationTokenSource();
		}
		book.IsInLibrary = await processEpubFiles.ProcessFileAsync(book, cancellationTokenSource.Token).ConfigureAwait(false);
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
		if(Books.Count > 0)
		{
			Books.Clear();
			logger.Warn("Books are already loaded, clearing list and continuing.");
		}

		if (cancellationTokenSource.IsCancellationRequested)
		{
			cancellationTokenSource = new CancellationTokenSource();
			logger.Info("Cancellation token source reset.");
		}

		try
		{
			WeakReferenceMessenger.Default.Register<BookMessage>(this, (r, m) => OnAddBooks(m.Value));
			logger.Info("Initializing Url...");
			LoadPopup();
			var (ipAddress, port) = await InitializeIpAddress().ConfigureAwait(true);
			var db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException("Database service is not available.");
			var settings = await db.GetSettings() ?? new Settings();
			var urlPrefix = settings.UrlPrefix;
			var prefix = urlPrefix.ToLowerInvariant() switch
			{
				"http" => "http",
				"https" => "https",
				_ => "http"
			};
			var url = $"{prefix}://{ipAddress}:{port}";
			System.Diagnostics.Debug.WriteLine($"Using URL: {url}");
			System.Diagnostics.Debug.WriteLine($"Using IP address: {ipAddress}, Port: {port}, prefix: {prefix}");
			var testUrl = $"{prefix}://{ipAddress}";
			if (!await ValidateUrl(testUrl, prefix))
			{
				logger.Warn($"Invalid URL: {testUrl}");
				return;
			}

			logger.Info($"Base URL initialized to {prefix}://{ipAddress}:{port}");
			logger.Info("Loading books from Calibre server...");
			await LoadCalibreDataFromUrl(ipAddress, port, prefix);
			logger.Info("Books loaded successfully from Calibre server.");
			
			popup.Closed += (s, e) =>
			{
				logger.Info("File dialog popup closed.");
				WeakReferenceMessenger.Default.UnregisterAll(this);
			};
			await popup.CloseAsync(cancellationTokenSource.Token);

		}
		catch (Exception ex)
		{
			logger.Error($"An error occurred while creating the popup dialog: {ex.Message}");
		}
		finally
		{
			WeakReferenceMessenger.Default.UnregisterAll(this);
			logger.Info("LoadBooks completed successfully.");
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
			logger.Info($"Using local or permitted external URL address: {url}");
		}
		else
		{
			// If the URL is not local and/or does not use https, log a warning and use localhost as a fallback

			logger.Warn($"URL address {url} is not local or permitted external. Using default base URL.");
			EmptyLabelText = "Web Address must be local\nif using http: Please upgrade to https\nin order to access a\ncalibre server on the\ninternet!";
			return false;
		}

		if (prefix.Equals("https") && !await ValidateSSLCerticate(url))
		{
			logger.Error($"SSL certificate validation failed for {url}");
			EmptyLabelText = "SSL certificate validation failed.\nPlease check your Calibre server settings.";
			return false;
		}
		logger.Info($"URL {url} is valid and accessible.");
		return true;
	}

	/// <summary>
	/// Validates the SSL certificate of the specified URL by attempting an HTTP GET request.
	/// </summary>
	/// <remarks>This method performs an HTTP GET request to the specified URL using a custom server certificate
	/// validation callback. It logs any errors encountered during the request and returns <see langword="false"/> if the
	/// response is empty or if an exception occurs.</remarks>
	/// <param name="url">The URL of the server whose SSL certificate is to be validated.</param>
	/// <returns><see langword="true"/> if the SSL certificate is valid and the server responds successfully; otherwise, <see
	/// langword="false"/>.</returns>
	async Task<bool> ValidateSSLCerticate(string url)
	{
		HttpClientHandler handler = new()
		{
			ServerCertificateCustomValidationCallback = NetworkChecker.ServerCertificateCustomValidation
		};
		HttpClient client = new(handler)
		{
			Timeout = TimeSpan.FromSeconds(10)
		};
		try
		{
			HttpResponseMessage response = await client.GetAsync(url, cancellationTokenSource.Token);
			response.EnsureSuccessStatusCode();

			string responseBody = await response.Content.ReadAsStringAsync(cancellationTokenSource.Token);
			if (string.IsNullOrEmpty(responseBody))
			{
				logger.Warn("Received empty response from Calibre server.");
				EmptyLabelText = "Received empty response from Calibre server.";
				return false;
			}
		}
		catch (HttpRequestException e)
		{
			logger.Error($"Error connecting to Calibre server at {url}: {e.Message}");
			EmptyLabelText = $"Error connecting to Calibre server at {url}. Please check your settings.";
			return false;
		}
		finally
		{
			handler.Dispose();
			client.Dispose();
		}
		return true;
	}
	void LoadPopup()
	{
		popup = new FileDialogePage(new FileDialogePageViewModel());
		Shell.Current.ShowPopup(popup, options);
	}
	async Task LoadCalibreDataFromUrl(string ipAddress, int port, string prefix)
	{
		var url = $"{prefix}://{ipAddress}:{port}/mobile";
		
		if (!await NetworkChecker.ValidateNetworkConnection(url))
		{
			logger.Warn($"Network connection failed for {url}");
			EmptyLabelText = "Network connection failed. Please check your settings.";
			return;
		}

		if (!NetworkChecker.IsAddressLocalOrPermittedExternal(url))
		{
			logger.Warn($"URL address {url} is not local or permitted external. Using default base URL.");
			EmptyLabelText = "Web Address must be local\nif using http: Please upgrade to https\nin order to access a\ncalibre server on the\ninternet!";
			return;
		}

		if (!await ValidateSSLCerticate(url))
		{
			logger.Error($"SSL certificate validation failed for {url}");
			EmptyLabelText = "SSL certificate validation failed.\nPlease check your Calibre server settings.";
			return;
		}
		
		logger.Info($"Loading books from Calibre server at {url}");
		// Check if the URL is valid and accessible
		


		int numberOfBooks = await calibreScraper.GetTotalBooksAsync(url);
		int count = 0;
		
		await foreach (var book in calibreScraper.GetBooksAsyncEnumerable(url, cancellationTokenSource.Token))
		{
			if(cancellationTokenSource.IsCancellationRequested)
			{
				Cancelled = true;
				logger.Info("Loading books cancelled by user.");
				await popup.CloseAsync(cancellationTokenSource.Token);
				break;
			}
			var folderinfo = new FileInfo
			{
				Count = count,
				MaxCount = numberOfBooks,
				Title = book.Title
			};
#if WINDOWS || MACCATALYST
			if(count % 100 == 0)
			{
				WeakReferenceMessenger.Default.Send(new FileMessage(folderinfo));
			}
#endif
#if ANDROID || IOS
			if(count % 5 == 0)
			{
				WeakReferenceMessenger.Default.Send(new FileMessage(folderinfo));
			}
#endif
			book.IsInLibrary = await processEpubFiles.IsBookAlreadyInLibrary(book);
			Books.Add(book);
			count++;
		}
		logger.Info($"Loaded {count} books from Calibre server at {url}");
	}
	void OnAddBooks(Book book)
	{
		if (book is not null)
		{
			if (Books.Any(b => b.Title == book.Title))
			{
				logger.Info($"Book already exists in library: {book.Title}");
				return;
			}

			//book.IsInLibrary = true; // Ensure the book is marked as in library
			book.IsInLibrary = true; // Mark the book as in library
			Books.Add(book);
			logger.Info($"Book message received: {book.Title}");
		}
		else
		{
			logger.Warn("Received null book message");
		}
	}
}
