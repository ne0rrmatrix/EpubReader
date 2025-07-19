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
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(CalibrePageViewModel));
#pragma warning disable S1075 // URIs should not be hardcoded
	static string baseUrl = "http://localhost:8080"; // Replace with your actual Calibre server URL
#pragma warning restore S1075 // URIs should not be hardcoded
	readonly CalibreScraper calibreScraper = new();
	CancellationTokenSource cancellationTokenSource = new();
	readonly bool isLoaded = false;
	readonly Popup settingsPopup = new CalibreSettingsPage(new CalibreSettingsPageViewModel());
	readonly Popup popup = new FileDialogePage(new FileDialogePageViewModel());
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
		List<(string IpAddress, int Port)> servers = [];
		if (settings.CalibreAutoDiscovery)
		{
			servers = await CalibreZeroConf.DiscoverCalibreServers().ConfigureAwait(false);
			baseUrl = $"http://{servers[0].IpAddress}:{servers[0].Port}";
		}
	
		if(servers.Count > 0)
		{
			logger.Info($"Using discovered Calibre server at {baseUrl}");
		}
		else
		{
			servers.Clear();
			servers.Add((settings.IPAddress, settings.Port));
			baseUrl = $"http://{settings.IPAddress}:{settings.Port}";
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
		book.IsInLibrary = true; // Ensure the book is marked as in library
		await processEpubFiles.ProcessFileAsync(book, cancellationTokenSource.Token).ConfigureAwait(false);
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
			logger.Warn("Books are already loaded, skipping load operation.");
			return;
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
			logger.Info($"Base URL initialized to http://{ipAddress}:{port}");
			logger.Info("Loading books from Calibre server...");
			await LoadCalibreDataFromUrl(ipAddress, port);
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
	void LoadPopup()
	{
		Shell.Current.ShowPopup(popup, options);
	}
	async Task LoadCalibreDataFromUrl(string ipAddress, int port)
	{
		var url = $"http://{ipAddress}:{port}/mobile";
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
			if(count % 100 == 0)
			{
				WeakReferenceMessenger.Default.Send(new FileMessage(folderinfo));
			}
			book.IsInLibrary = await processEpubFiles.IsBookAlreadyInLibrary(book);
			Books.Add(book);
			count++;
		}
		logger.Info($"Loaded {count} books from Calibre server at {url}");
	}
	void OnAddBooks(Book value)
	{
		if (value is not null)
		{
			var ebook = value;
			if (Books.Any(b => b.Title == ebook.Title))
			{
				logger.Info($"Book already exists in library: {ebook.Title}");
				return;
			}

			value.IsInLibrary = true; // Ensure the book is marked as in library
			ebook.IsInLibrary = true; // Mark the book as in library
			Books.Add(ebook);
			logger.Info($"Book message received: {ebook.Title}");
		}
		else
		{
			logger.Warn("Received null book message");
		}
	}
}
