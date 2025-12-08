using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui.Extensions;
using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.Util;

namespace EpubReader.ViewModels;

/// <summary>
/// Represents the view model for managing a library of books, providing functionality to add, remove, and navigate books.
/// </summary>
/// <remarks>
/// The <see cref="LibraryViewModel"/> class is responsible for handling operations related to a collection of books,
/// including adding books from files, removing books, and navigating to a book's page. It interacts with services
/// for file picking, database operations, and ebook processing.
/// </remarks>
public partial class LibraryViewModel : BaseViewModel
{
	bool isAlphabeticalSorted = true;

	Popup popup = new FolderDialogePage(new FolderDialogPageViewModel());
	readonly PopupOptions options = new()
	{
		CanBeDismissedByTappingOutsideOfPopup = false,
	};

	/// <summary>
	/// Provides a read-only instance of the <see cref="ProcessEpubFiles"/> service.
	/// </summary>
	/// <remarks>This field retrieves the <see cref="ProcessEpubFiles"/> service from the current application's
	/// service provider. If the service cannot be resolved, an <see cref="InvalidOperationException"/> is
	/// thrown.</remarks>
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	readonly ISyncService syncService = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ISyncService>() ?? throw new InvalidOperationException();

	/// <summary>
	/// Gets or sets the collection of books in the library.
	/// </summary>
	[ObservableProperty]
	public partial ObservableCollection<Book> Books { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LibraryViewModel"/> class.
	/// </summary>
	public LibraryViewModel()
	{
		Books ??= [];

		WeakReferenceMessenger.Default.Register<CalibreMessage>(this, (r, m) =>
		{
			if (m.Value)
			{
				CancellationTokenSource.Cancel();
			}
		});
		WeakReferenceMessenger.Default.Register<BookMessage>(this, (r, m) =>
		{
			if (CancellationTokenSource.IsCancellationRequested)
			{
				CancellationTokenSource = new CancellationTokenSource();
			}
			var ebook = m.Value;
			if (Books.Any(b => b.Title == ebook.Title))
			{
				Logger.Info($"Book already exists in library: {ebook.Title}");
				return;
			}
			Book.IsInLibrary = true; // Ensure the book is marked as in library
			Books.Add(ebook);
			Logger.Info($"Book message received: {ebook.Title}");
		});
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			CancellationTokenSource?.Dispose();
			WeakReferenceMessenger.Default.UnregisterAll(this);
		}
		base.Dispose(disposing);
	}

	#region Commands

	/// <summary>
	/// Navigates to the book page asynchronously, opening the specified book and setting its current state.
	/// </summary>
	/// <param name="book">The book to open and navigate to. Must not be <see langword="null"/>.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	/// <exception cref="InvalidOperationException">Thrown if there is an error opening the ebook.</exception>
	[RelayCommand]
	public async Task GotoBookPageAsync(Book book)
	{
		try
		{
			var existingBook = await db.GetBook(book);
			ArgumentNullException.ThrowIfNull(existingBook);
			existingBook.SyncId = await BookIdentityService.ComputeSyncIdAsync(existingBook, CancellationTokenSource.Token);
			await db.SaveBookData(existingBook, CancellationTokenSource.Token);

			Book = await EbookService.OpenEbookAsync(book.FilePath).ConfigureAwait(true)
				?? throw new InvalidOperationException("Error opening ebook");

			Book.SyncId = existingBook.SyncId;
			// Restore reading position from sync service (handles local cache and cloud)
			await RestoreProgressAsync(existingBook);
			Book.Id = existingBook.Id;

			StreamExtensions.Instance?.SetBook(Book);

			var navigationParams = new Dictionary<string, object>
			{
				{ "Book", Book }
			};

			await Shell.Current.GoToAsync("BookPage", navigationParams);
		}
		catch (Exception ex)
		{
			Logger.Error($"Error navigating to book page: {ex.Message}");
			await ShowErrorToastAsync("Error opening book. Please try again.");
		}
	}

	async Task RestoreProgressAsync(Book existingBook)
	{
		var token = CancellationTokenSource.Token;
		var syncId = await BookIdentityService.ComputeSyncIdAsync(existingBook, token);

		// Get progress (sync service checks local cache first, then cloud)
		var progress = await syncService.GetProgressAsync(syncId, token);

		// Backfill from legacy Book fields if no progress record yet
		if (progress is null && (existingBook.CurrentChapter > 0 || existingBook.CurrentPage > 0))
		{
			progress = new ReadingProgress
			{
				BookId = syncId,
				CurrentChapter = existingBook.CurrentChapter,
				CurrentPage = existingBook.CurrentPage,
				LastUpdated = DateTimeOffset.UtcNow.ToString("o"),
				DeviceId = string.Empty,
				DeviceName = string.Empty,
				IsSynced = false
			};
			await syncService.SaveProgressAsync(progress, token);
		}

		if (progress is not null)
		{
			Book.CurrentChapter = progress.CurrentChapter;
			Book.CurrentPage = progress.CurrentPage;
		}
	}

	/// <summary>
	/// Removes the specified book from the library.
	/// </summary>
	/// <param name="book">The book to be removed.</param>
	[RelayCommand]
	public async Task RemoveBook(Book book)
	{
		try
		{
			ArgumentNullException.ThrowIfNull(book);
			Logger.Info($"Removing book: {book.Title}");

			var directory = Path.GetDirectoryName(book.FilePath);
			if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
			{
				Directory.Delete(directory, true);
			}

			await db.RemoveBook(book, CancellationTokenSource.Token);
			Books.Remove(book);

			Logger.Info("Book removed from library");
		}
		catch (Exception ex)
		{
			Logger.Error($"Error removing book: {ex.Message}");
		}
	}

	/// <summary>
	/// Sorts the collection of books in alphabetical order by the first author's name.
	/// </summary>
	/// <remarks>This method orders the books based on the name of the first author in each book's author list,
	/// using a case-insensitive comparison. The sorted order is applied directly to the existing collection.</remarks>
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
	/// Initiates the Calibre integration process asynchronously.
	/// </summary>
	/// <returns>A task that represents the asynchronous operation. Currently, this task completes immediately as the method is a
	/// placeholder for future implementation.</returns>
	[RelayCommand]
	async Task Calibre()
	{
		if (CancellationTokenSource.IsCancellationRequested)
		{
			CancellationTokenSource = new CancellationTokenSource();
		}
		await Shell.Current.GoToAsync("CalibrePage").WaitAsync(CancellationTokenSource.Token);
	}

	/// <summary>
	/// Asynchronously adds EPUB files from a selected folder to the library.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	[RelayCommand]
	public async Task AddFolderAsync()
	{
		if (CancellationTokenSource.IsCancellationRequested)
		{
			CancellationTokenSource = new CancellationTokenSource();
		}

		var folderUri = await processEpubFiles.FolderPicker.PickFolderAsync();
		if (string.IsNullOrEmpty(folderUri))
		{
			Logger.Info("No folder selected");
			return;
		}

		Logger.Info($"Selected folder: {folderUri}");
		popup.Closed += (s, e) =>
		{
			Logger.Info("File dialog popup closed.");
			WeakReferenceMessenger.Default.UnregisterAll(this);
		};
		LoadPopup();

		var epubFiles = await processEpubFiles.FolderPicker.EnumerateEpubFilesInFolderAsync(folderUri, CancellationTokenSource.Token).ConfigureAwait(false);

		Logger.Info($"Found {epubFiles.Count} EPUB files in the selected folder");

		if (CancellationTokenSource.Token.IsCancellationRequested)
		{
			Logger.Info("Operation cancelled by user.");
			await Dispatcher.DispatchAsync(async () => { await popup.CloseAsync(); });
			return;
		}

		await processEpubFiles.ProcessEpubFilesAsync(epubFiles, CancellationTokenSource.Token).ConfigureAwait(false);
		await Dispatcher.DispatchAsync(async () => { await popup.CloseAsync(); });
	}

	/// <summary>
	/// Loads and displays a folder dialog popup.
	/// </summary>
	/// <remarks>This method initializes a new folder dialog page and displays it as a popup using the current
	/// shell.</remarks>
	void LoadPopup()
	{
		popup = new FolderDialogePage(new FolderDialogPageViewModel());
		Shell.Current.ShowPopup(popup, options);
	}

	/// <summary>
	/// Unregisters all messages associated with this instance from the default messenger.
	/// </summary>
	/// <remarks>This method should be called to clean up message registrations when the page is no longer needed,
	/// preventing potential memory leaks by ensuring that this instance is no longer referenced by the
	/// messenger.</remarks>
	public void UnloadPage()
	{
		WeakReferenceMessenger.Default.UnregisterAll(this);
	}

	/// <summary>
	/// Asynchronously adds a selected EPUB book to the library.
	/// </summary>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[RelayCommand]
	public async Task AddAsync(CancellationToken cancellationToken = default)
	{
		if (CancellationTokenSource.IsCancellationRequested)
		{
			CancellationTokenSource = new CancellationTokenSource();
		}
		try
		{

			var result = await processEpubFiles.PickEpubFileAsync(cancellationToken).ConfigureAwait(false);
			if (result is null)
			{
				Logger.Info("No file selected");
				return;
			}

			var ebook = await EbookService.GetListingAsync(result.FullPath).ConfigureAwait(false);
			if (ebook is null)
			{
				await ShowErrorToastAsync("Error opening book. Please select a valid EPUB file.");
				return;
			}

			if (await processEpubFiles.IsBookAlreadyInLibrary(ebook))
			{
				await ShowInfoToastAsync($"Book already exists in library: {ebook.Title}");
				return;
			}

			await processEpubFiles.SaveBookToLibraryAsync(ebook, result, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.Error($"Error adding book: {ex.Message}");
			await ShowErrorToastAsync("Error adding book. Please try again.");
		}
	}
	#endregion

}

