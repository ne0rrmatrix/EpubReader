using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
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
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(LibraryViewModel));

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
		Books = [];
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
		if (value is not null)
		{
			var ebook = value;
			if (Books.Any(b => b.Id == ebook.Id))
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
			logger.Error($"Error navigating to book page: {ex.Message}");
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

			logger.Info($"Removing book: {book.Title}");

			DeleteBookFiles(book);
			db.RemoveBook(book);
			Books.Remove(book);

			logger.Info("Book removed from library");
		}
		catch (Exception ex)
		{
			logger.Error($"Error removing book: {ex.Message}");
		}
	}

	/// <summary>
	/// Deletes the files associated with a book.
	/// </summary>
	/// <param name="book">The book whose files should be deleted.</param>
	static void DeleteBookFiles(Book book)
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
			logger.Error($"Error deleting book files: {ex.Message}");
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
		if (string.IsNullOrWhiteSpace(searchText))
		{
			logger.Info("Search text is empty, showing all books");
			Books = new ObservableCollection<Book>(db.GetAllBooks().Result);
			return;
		}
		logger.Info($"Searching for books with author containing: {searchText}");
		var filteredBooks = await db.GetAllBooks();
			filteredBooks = [.. filteredBooks.Where(b => b.Author.Contains(searchText, StringComparison.OrdinalIgnoreCase))];
		if (filteredBooks.Count == 0)
		{
			logger.Info("No books found matching the search criteria");
			await ShowInfoToastAsync("No books found matching your search criteria").ConfigureAwait(false);
		}
		Books = new ObservableCollection<Book>(filteredBooks);
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
		if (string.IsNullOrWhiteSpace(searchText))
		{
			logger.Info("Search text is empty, showing all books");
			Books = new ObservableCollection<Book>(db.GetAllBooks().Result);
			return;
		}
		logger.Info($"Searching for books with title containing: {searchText}");
		var filteredBooks = await db.GetAllBooks();
			filteredBooks = [.. filteredBooks.Where(b => b.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase))];
		if (filteredBooks.Count == 0)
		{
			logger.Info("No books found matching the search criteria");
			await ShowInfoToastAsync("No books found matching your search criteria").ConfigureAwait(false);
		}
		Books = new ObservableCollection<Book>(filteredBooks);
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
			logger.Info("Sorting books by author");
			var sortedBooks = Books.OrderBy(b => b.Author, StringComparer.OrdinalIgnoreCase).ToList();
			Books.Clear();
			foreach (var book in sortedBooks)
			{
				Books.Add(book);
			}
			logger.Info("Alphabetical author sort disabled, no changes made");
			return;
		}
		logger.Info("Sorting books by reverse Alphabetical sort");
		var reverseAlphabeticalBooks = Books.OrderByDescending(b => b.Author, StringComparer.OrdinalIgnoreCase).ToList();
		Books.Clear();
		foreach (var book in reverseAlphabeticalBooks)
		{
			Books.Add(book);
		}
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
			logger.Info("Sorting books by title");
			var sortedBooks = Books.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase).ToList();
			Books.Clear();
			foreach (var book in sortedBooks)
			{
				Books.Add(book);
			}
			return;
		}
		logger.Info("Sorting books by reverse title");
		var reverseSortedBooks = Books.OrderByDescending(b => b.Title, StringComparer.OrdinalIgnoreCase).ToList();
		Books.Clear();
		foreach (var book in reverseSortedBooks)
		{
			Books.Add(book);
		}
	}

	/// <summary>
	/// Asynchronously adds EPUB files from a selected folder to the library.
	/// </summary>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[RelayCommand]
	public async Task AddFolderAsync(CancellationToken cancellationToken = default)
	{
		WeakReferenceMessenger.Default.Register<BookMessage>(this, (r, m) => OnAddBooks(m.Value));
		var popup = new FolderDialogePage(new FolderDialogePageViewModel());
		PopupOptions options = new()
		{
			CanBeDismissedByTappingOutsideOfPopup = false,
		};

		try
		{
			var folderUri = await processEpubFiles.FolderPicker.PickFolderAsync();
			if (string.IsNullOrEmpty(folderUri))
			{
				logger.Info("No folder selected");
				return;
			}

			logger.Info($"Selected folder: {folderUri}");
			var epubFiles = await processEpubFiles.FolderPicker.EnumerateEpubFilesInFolderAsync(folderUri);

			if (epubFiles.Count == 0)
			{
				await ShowInfoToastAsync("No EPUB files found in the selected folder", cancellationToken);
				return;
			}

			logger.Info($"Found {epubFiles.Count} EPUB files in the selected folder");
			var navigationParams = new Dictionary<string, object>
			{
				{ "Epubfiles", epubFiles }
			};

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
					});
					
				}
				else
				{
					logger.Warn("Received null book message");
				}
			});
			var result = await Shell.Current.ShowPopupAsync(popup, options, navigationParams, cancellationToken);
			if (result is not null)
			{
				// Process the result if needed
				logger.Info("Folder dialog popup closed successfully");
			}
		}
		catch (Exception ex)
		{
			logger.Error($"Error adding folder: {ex.Message}");
			await ShowErrorToastAsync("Error processing folder. Please try again.", cancellationToken);
		}
		finally
		{
			WeakReferenceMessenger.Default.UnregisterAll(this);
		}
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
			WeakReferenceMessenger.Default.Register<BookMessage>(this, (r, m) => OnAddBooks(m.Value));
			var result = await processEpubFiles.PickEpubFileAsync().ConfigureAwait(false);
			if (result is null)
			{
				logger.Info("No file selected");
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
			logger.Error($"Error adding book: {ex.Message}");
			await ShowErrorToastAsync("Error adding book. Please try again.", cancellationToken);
		}
		finally
		{
			WeakReferenceMessenger.Default.UnregisterAll(this);
		}
	}
	#endregion
	#region Toast Helper Methods

	/// <summary>
	/// Shows an informational toast message.
	/// </summary>
	/// <param name="message">The message to display.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	async Task ShowInfoToastAsync(string message, CancellationToken cancellationToken = default)
	{
		await Dispatcher.DispatchAsync(async () =>
			await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
		logger.Info(message);
	}

	/// <summary>
	/// Shows an error toast message.
	/// </summary>
	/// <param name="message">The message to display.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	async Task ShowErrorToastAsync(string message, CancellationToken cancellationToken = default)
	{
		await Dispatcher.DispatchAsync(async () =>
			await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
		logger.Error(message);
	}

	#endregion
}
