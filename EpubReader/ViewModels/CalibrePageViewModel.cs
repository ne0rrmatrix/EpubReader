using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Util;
using EpubReader.Views;
using MetroLog;
using FileInfo = EpubReader.Models.FileInfo;
namespace EpubReader.ViewModels;
public partial class CalibrePageViewModel : BaseViewModel
{
	//TODO: Replace with your actual Calibre server URL and implement a method to set the base URL dynamically if needed
	//TODO: Add error handling for network requests and data parsing
	//TODO: Implement a method to download the book file and save it to local storage
	//TODO: Implement a method to display a popup dialog for downloading the book file
	//TODO: Implement a method to handle the book download process, including progress tracking and error handling
	//TODO: Implement a method to refresh the book list from the Calibre server
	//TODO: Implement a method to cancel the Calibre server download process if needed

	/// <summary>
	/// Provides a read-only instance of the <see cref="ProcessEpubFiles"/> service.
	/// </summary>
	/// <remarks>This field retrieves the <see cref="ProcessEpubFiles"/> service from the current application's
	/// service provider. If the service cannot be resolved, an <see cref="InvalidOperationException"/> is
	/// thrown.</remarks>
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(CalibrePageViewModel));
	const string baseUrl = "http://localhost:8080"; // Replace with your actual Calibre server URL
	readonly CalibreScraper calibreScraper = new(baseUrl);

	/// <summary>
	/// Gets or sets a value indicating whether the operation has been cancelled.
	/// </summary>
	[ObservableProperty]
	public partial bool Cancelled { get; set; } = false;
	readonly CancellationTokenSource cancellationTokenSource = new();

	[ObservableProperty]
	public partial ObservableCollection<Book> Books { get; set; }
	public CalibrePageViewModel()
	{
		Books = [];
		WeakReferenceMessenger.Default.Register<BookMessage>(this, (r, m) => OnAddBooks(m.Value));
	}


	/// <summary>
	/// Adds a book to the collection of books.
	/// </summary>
	/// <remarks>This method adds the specified <paramref name="book"/> to the <see cref="Book"/>
	/// collection.</remarks>
	/// <param name="book">The book to add to the collection. Cannot be null.</param>
	/// <returns></returns>
	[RelayCommand]
	public async Task AddBook(Book book, CancellationToken cancellationToken = default)
	{
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

	public async Task LoadBooks(CancellationToken cancellationToken = default)
	{
		var popup = new FileDialogePage(new FileDialogePageViewModel());
		PopupOptions options = new()
		{
			CanBeDismissedByTappingOutsideOfPopup = false,
		};
		
		try
		{
			ThreadPool.QueueUserWorkItem(async (item) =>
			{
				try
				{
					await LoadCalibreDataFromUrl().ConfigureAwait(false);
					WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
				}
				catch (Exception ex)
				{
					logger.Error($"An error occurred while loading data: {ex.Message}");
				}
			});
			WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) =>
			{
				System.Diagnostics.Debug.WriteLine($"SettingsMessage received: {m.Value}");
				if (m.Value)
				{
					MainThread.BeginInvokeOnMainThread(async () =>
					{
						var tempResult = await Shell.Current.ClosePopupAsync(popup, cancellationToken);
						if (tempResult.Result is not null)
						{
							logger.Info("Folder dialog popup closed successfully");
						}
						else
						{
							logger.Warn("Folder dialog popup was not closed successfully");
						}
						logger.Info("SettingsMessage received, closing popup");
						WeakReferenceMessenger.Default.Unregister<SettingsMessage>(this);
					});

				}
				else
				{
					logger.Warn("Received null book message");
				}
			});
			var result = await Shell.Current.ShowPopupAsync(popup, options, cancellationToken);
			
			if (result is not null)
			{
				logger.Info("File dialog popup closed successfully");
			}
			else
			{
				logger.Warn("File dialog popup was not closed successfully");
			}
			
			logger.Info("LoadBooks completed successfully.");
		}
		catch (Exception ex)
		{
			logger.Error($"An error occurred while creating the popup dialog: {ex.Message}");
			return;
		}
		
	}
	
	async Task LoadCalibreDataFromUrl()
	{
		try
		{
			var url = baseUrl + "/mobile";
			int numberOfBooks = await calibreScraper.GetTotalBooksAsync(url);
			int count = 0;
			await foreach (var book in calibreScraper.GetBooksAsyncEnumerable(cancellationTokenSource.Token))
			{
				var folderinfo = new FileInfo
				{
					Count = count,
					MaxCount = numberOfBooks,
					Title = book.Title
				};
				WeakReferenceMessenger.Default.Send(new FileMessage(folderinfo));
				Books.Add(book);
				count++;
			}
			if (count == 0)
			{
				logger.Warn("No books were loaded from the Calibre server.");
			}
			if (count == numberOfBooks)
			{
				logger.Info($"All {numberOfBooks} books were successfully loaded from the Calibre server.");
			}
			else
			{
				logger.Warn($"Expected {numberOfBooks} books, but only loaded {count} books.");
			}
			logger.Info("LoadCalibreDataFromUrl completed successfully.");
			
		}
		catch (Exception ex)
		{
			logger.Error($"An error occurred while loading data: {ex.Message}");
			throw;
		}
		finally
		{
			logger.Info("LoadCalibreDataFromUrl completed.");
		}
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
			Books.Add(ebook);
			logger.Info($"Book message received: {ebook.Title}");
		}
		else
		{
			logger.Warn("Received null book message");
		}
	}
}
