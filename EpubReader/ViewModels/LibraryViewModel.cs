using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.Util;
using EpubReader.Views;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;

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
	CancellationTokenSource cancellationTokenSource = new();
	Popup popup = new FolderDialogePage(new FolderDialogPageViewModel());
	readonly PopupOptions options = new()
	{
		CanBeDismissedByTappingOutsideOfPopup = false,
	};
	bool isAlphabeticalSorted = true;

	/// <summary>
	/// Provides a read-only instance of the <see cref="ProcessEpubFiles"/> service.
	/// </summary>
	/// <remarks>This field retrieves the <see cref="ProcessEpubFiles"/> service from the current application's
	/// service provider. If the service cannot be resolved, an <see cref="InvalidOperationException"/> is
	/// thrown.</remarks>
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	
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
				cancellationTokenSource.Cancel();
			}
		});
		WeakReferenceMessenger.Default.Register<BookMessage>(this, (r, m) => OnAddBooks(m.Value));
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			cancellationTokenSource?.Dispose();
			WeakReferenceMessenger.Default.UnregisterAll(this);
		}
		base.Dispose(disposing);
	}

	/// <summary>
	/// Adds a book to the library if it does not already exist.
	/// </summary>
	/// <remarks>If the book already exists in the library, it will not be added again, and an informational log
	/// entry will be created. If the provided book is <see langword="null"/>, a warning log entry will be
	/// generated.</remarks>
	/// <param name="value">The book to be added. Cannot be <see langword="null"/>.</param>
	void OnAddBooks(Book value)
	{
		if(cancellationTokenSource.IsCancellationRequested)
		{
			cancellationTokenSource = new CancellationTokenSource();
		}
		var ebook = value;
		if (Books.Any(b => b.Title == ebook.Title))
		{
			Logger.Info($"Book already exists in library: {ebook.Title}");
			return;
		}
		Book.IsInLibrary = true; // Ensure the book is marked as in library
		Books.Add(ebook);
		Logger.Info($"Book message received: {ebook.Title}");
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

			Book = await EbookService.OpenEbookAsync(book.FilePath).ConfigureAwait(true)
				?? throw new InvalidOperationException("Error opening ebook");

			// Restore reading position
			Book.CurrentChapter = existingBook.CurrentChapter;
			Book.CurrentPage = existingBook.CurrentPage;
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

	/// <summary>
	/// Removes the specified book from the library.
	/// </summary>
	/// <param name="book">The book to be removed.</param>
	[RelayCommand]
	public void RemoveBook(Book book)
	{
		try
		{
			ArgumentNullException.ThrowIfNull(book);

			Logger.Info($"Removing book: {book.Title}");
			
			DeleteBookFiles(book);
			db.RemoveBook(book);
			Books.Remove(book);

			Logger.Info("Book removed from library");
		}
		catch (Exception ex)
		{
			Logger.Error($"Error removing book: {ex.Message}");
		}
	}

	/// <summary>
	/// Deletes the files associated with a book.
	/// </summary>
	/// <param name="book">The book whose files should be deleted.</param>
	void DeleteBookFiles(Book book)
	{
		try
		{
			var directory = Path.GetDirectoryName(book.FilePath);
			if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
			{
				Directory.Delete(directory, true);
			}
		}
		catch (Exception ex)
		{
			Logger.Error($"Error deleting book files: {ex.Message}");
		}
	}

	/// <summary>
	/// Searches for books with authors whose names contain the specified search text.
	/// </summary>
	/// <remarks>If no books are found matching the search criteria, an informational message is displayed to the
	/// user.</remarks>
	/// <param name="searchText">The text to search for within author names. If null or whitespace, all books are shown.</param>
	/// <returns></returns>
	[RelayCommand]
	public async Task SearchAuthorName(string searchText)
	{
		var allBooks = await db.GetAllBooks();
		if (string.IsNullOrWhiteSpace(searchText))
		{
			Logger.Info("Search text is empty, showing all books");
			if (Books.Count == allBooks.Count)
			{
				Logger.Info("No changes made, books already loaded");
				return;
			}
			Books = [.. allBooks];
		}

		Logger.Info($"Searching for books with author containing: {searchText}");
		
		var filteredBooks = allBooks.Where(b => b.Author.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();

		Books = [.. filteredBooks];
	}

	/// <summary>
	/// Searches for books with titles containing the specified search text and updates the book collection accordingly.
	/// </summary>
	/// <remarks>If the <paramref name="searchText"/> is null or consists only of whitespace, the method retrieves
	/// and displays all available books. Otherwise, it filters the books to include only those whose titles contain the
	/// specified text, ignoring case. If no books match the search criteria, an informational message is displayed to the
	/// user.</remarks>
	/// <param name="searchText">The text to search for within book titles. If null or whitespace, all books are shown.</param>
	/// <returns></returns>
	[RelayCommand]
	public async Task SearchBookName(string searchText)
	{
		var allBooks = await db.GetAllBooks();
		if (string.IsNullOrWhiteSpace(searchText))
		{
			Logger.Info("Search text is empty, showing all books");
			if (Books.Count == allBooks.Count)
			{
				Logger.Info("No changes made, books already loaded");
				return;
			}
			Books = [.. allBooks];
		}

		Logger.Info($"Searching for books with title containing: {searchText}");
		var filteredBooks = allBooks.Where(b => b.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
		
		Books = [.. filteredBooks];
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

		if(!isAlphabeticalSorted)
        {
			Logger.Info("Sorting books by author");
			Books = [.. Books.OrderBy(b => b.Author, StringComparer.OrdinalIgnoreCase)];
			Logger.Info("Alphabetical author sort disabled, no changes made");
			return;
        }

		Logger.Info("Sorting books by reverse Alphabetical sort");
		Books = [.. Books.OrderByDescending(b => b.Author, StringComparer.OrdinalIgnoreCase)];
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
		if(!isAlphabeticalSorted)
		{
			Logger.Info("Sorting books by title");
			Books = [.. Books.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase)];
			return;
		}

		Logger.Info("Sorting books by reverse title");
		Books = [.. Books.OrderByDescending(b => b.Title, StringComparer.OrdinalIgnoreCase)];
	}

	/// <summary>
	/// Initiates the Calibre integration process asynchronously.
	/// </summary>
	/// <returns>A task that represents the asynchronous operation. Currently, this task completes immediately as the method is a
	/// placeholder for future implementation.</returns>
	[RelayCommand]
	async Task Calibre()
	{
		if(cancellationTokenSource.IsCancellationRequested)
		{
			cancellationTokenSource = new CancellationTokenSource();
		}
		await Shell.Current.GoToAsync("CalibrePage").WaitAsync(cancellationTokenSource.Token);
	}

	/// <summary>
	/// Asynchronously adds EPUB files from a selected folder to the library.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	[RelayCommand]
	public async Task AddFolderAsync(CancellationToken cancellationToken = default)
	{
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

		var epubFiles = await processEpubFiles.FolderPicker.EnumerateEpubFilesInFolderAsync(folderUri, cancellationToken).ConfigureAwait(false);

		Logger.Info($"Found {epubFiles.Count} EPUB files in the selected folder");

		await processEpubFiles.ProcessEpubFilesAsync(epubFiles, cancellationTokenSource.Token).ConfigureAwait(false);
		if (cancellationTokenSource.Token.IsCancellationRequested)
		{
			WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
		}
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
				await ShowErrorToastAsync("Error opening book. Please select a valid EPUB file.", cancellationToken);
				return;
			}

			if (await processEpubFiles.IsBookAlreadyInLibrary(ebook))
			{
				await ShowInfoToastAsync($"Book already exists in library: {ebook.Title}", cancellationToken);
				return;
			}

			await processEpubFiles.SaveBookToLibraryAsync(ebook, result, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.Error($"Error adding book: {ex.Message}");
			await ShowErrorToastAsync("Error adding book. Please try again.", cancellationToken);
		}
	}
	#endregion
	
}

