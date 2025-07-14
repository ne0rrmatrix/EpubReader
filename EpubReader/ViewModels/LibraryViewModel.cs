using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.Util;
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
	#region Constants and Static Fields

	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(LibraryViewModel));
	static readonly string[] epubExtensions = [".epub"];
	static readonly string[] androidEpubTypes = ["application/epub+zip", ".epub"];
	static readonly string[] iOSEpubTypes = ["org.idpf.epub-container"];

	#endregion

	#region Fields

	readonly IFolderPicker folderPicker;
	readonly FilePickerFileType customFileType;
	readonly Task? initializationTask;
	#endregion

	#region Properties

	/// <summary>
	/// Gets or sets the collection of books in the library.
	/// </summary>
	[ObservableProperty]
	public partial ObservableCollection<Book> Books { get; set; }

	#endregion

	#region Constructor

	/// <summary>
	/// Initializes a new instance of the <see cref="LibraryViewModel"/> class.
	/// </summary>
	/// <remarks>
	/// This constructor initializes the <see cref="Books"/> collection with all available books
	/// retrieved from the database. If no books are found, the collection is initialized as empty.
	/// </remarks>
	public LibraryViewModel()
	{
		Books = [];
		folderPicker = GetFolderPickerService();
		customFileType = CreateCustomFileType();
		initializationTask = InitializeLibraryAsync();
		if(initializationTask?.IsFaulted == true || initializationTask?.IsCanceled == true)
		{
			logger.Error("Error initializing library: {Message}", initializationTask.Exception);
		}
	}

	async Task? InitializeLibraryAsync()
	{
		var temp = await db.GetAllBooks();
		Books = new ObservableCollection<Book>(temp ?? []);
	}

	#endregion

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
	/// Asynchronously adds EPUB files from a selected folder to the library.
	/// </summary>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[RelayCommand]
	public async Task AddFolderAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var folderUri = await folderPicker.PickFolderAsync();
			if (string.IsNullOrEmpty(folderUri))
			{
				logger.Info("No folder selected");
				return;
			}

			logger.Info($"Selected folder: {folderUri}");
			var epubFiles = await folderPicker.EnumerateEpubFilesInFolderAsync(folderUri);

			if (epubFiles.Count == 0)
			{
				await ShowInfoToastAsync("No EPUB files found in the selected folder", cancellationToken);
				return;
			}

			logger.Info($"Found {epubFiles.Count} EPUB files in the selected folder");
			await ProcessEpubFilesAsync(epubFiles, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.Error($"Error adding folder: {ex.Message}");
			await ShowErrorToastAsync("Error processing folder. Please try again.", cancellationToken);
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
			var result = await PickEpubFileAsync().ConfigureAwait(false);
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

			if (IsBookAlreadyInLibrary(ebook))
			{
				await ShowInfoToastAsync($"Book already exists in library: {ebook.Title}", cancellationToken);
				return;
			}

			await SaveBookToLibraryAsync(ebook, result, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.Error($"Error adding book: {ex.Message}");
			await ShowErrorToastAsync("Error adding book. Please try again.", cancellationToken);
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

	#endregion

	#region Private Helper Methods

	/// <summary>
	/// Gets the folder picker service from the application's service container.
	/// </summary>
	/// <returns>The folder picker service instance.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the service cannot be resolved.</exception>
	static IFolderPicker GetFolderPickerService()
	{
		return Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IFolderPicker>()
			?? throw new InvalidOperationException("IFolderPicker service not available");
	}

	/// <summary>
	/// Creates the custom file type configuration for EPUB files across different platforms.
	/// </summary>
	/// <returns>A configured FilePickerFileType for EPUB files.</returns>
	static FilePickerFileType CreateCustomFileType()
	{
		return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
		{
			{ DevicePlatform.iOS, iOSEpubTypes},
			{ DevicePlatform.MacCatalyst, iOSEpubTypes },
			{ DevicePlatform.Android, androidEpubTypes },
			{ DevicePlatform.WinUI, epubExtensions },
			{ DevicePlatform.Tizen, epubExtensions },
		});
	}

	/// <summary>
	/// Prompts the user to select an EPUB file.
	/// </summary>
	/// <returns>The selected file result, or null if cancelled.</returns>
	async Task<FileResult?> PickEpubFileAsync()
	{
		var options = new PickOptions
		{
			FileTypes = customFileType,
			PickerTitle = "Please select an EPUB book"
		};

		return await PickAndShowAsync(options).ConfigureAwait(false);
	}

	/// <summary>
	/// Processes a collection of EPUB files from a folder.
	/// </summary>
	/// <param name="epubFiles">The list of EPUB file paths to process.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	async Task ProcessEpubFilesAsync(List<string> epubFiles, CancellationToken cancellationToken)
	{
		foreach (var file in epubFiles)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			await ProcessSingleEpubFileAsync(file, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Processes a single EPUB file from a folder operation.
	/// </summary>
	/// <param name="filePath">The path to the EPUB file.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	async Task ProcessSingleEpubFileAsync(string filePath, CancellationToken cancellationToken)
	{
		try
		{
			var stream = await folderPicker.PerformFileOperationOnEpubAsync(filePath);
			if (stream is null)
			{
				logger.Info($"Failed to open stream for file: {filePath}");
				return;
			}

			using (stream)
			{
				var ebook = await EbookService.GetListingAsync(stream, filePath).ConfigureAwait(false);
				if (ebook is null)
				{
					await ShowErrorToastAsync($"Error opening book: {Path.GetFileName(filePath)}", cancellationToken);
					return;
				}

				if (IsBookAlreadyInLibrary(ebook))
				{
					await ShowInfoToastAsync($"Book already exists in library: {ebook.Title}", cancellationToken);
					return;
				}

				stream.Seek(0, SeekOrigin.Begin);
				await SaveBookToLibraryAsync(ebook, stream, filePath, cancellationToken);
			}
		}
		catch (Exception ex)
		{
			logger.Error($"Error processing file {filePath}: {ex.Message}");
			await ShowErrorToastAsync($"Error processing {Path.GetFileName(filePath)}", cancellationToken);
		}
	}

	/// <summary>
	/// Checks if a book is already in the library based on its title.
	/// </summary>
	/// <param name="ebook">The book to check.</param>
	/// <returns>True if the book already exists in the library, false otherwise.</returns>
	bool IsBookAlreadyInLibrary(Book ebook)
	{
		return Books.Any(x => string.Equals(x.Title, ebook.Title, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Saves a book to the library from a FileResult.
	/// </summary>
	/// <param name="ebook">The book to save.</param>
	/// <param name="fileResult">The file result containing the book data.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	async Task SaveBookToLibraryAsync(Book ebook, FileResult fileResult, CancellationToken cancellationToken)
	{
		try
		{
			ebook.FilePath = await FileService.SaveFileAsync(fileResult, ebook.FilePath, cancellationToken).ConfigureAwait(false);
			ebook.CoverImagePath = await FileService.SaveImageAsync(ebook.FilePath, ebook.CoverImage, cancellationToken).ConfigureAwait(false);

			if (ValidateBookFiles(ebook))
			{
				await db.SaveBookData(ebook, cancellationToken).ConfigureAwait(false);
				Books.Add(ebook);
				await ShowInfoToastAsync("Book added to library", cancellationToken);
				logger.Info($"Book added to library: {ebook.Title}");
			}
			else
			{
				await ShowErrorToastAsync($"Failed to save book: {ebook.Title}", cancellationToken);
			}
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving book to library: {ex.Message}");
			await ShowErrorToastAsync("Error saving book to library", cancellationToken);
		}
	}

	/// <summary>
	/// Saves a book to the library from a stream.
	/// </summary>
	/// <param name="ebook">The book to save.</param>
	/// <param name="stream">The stream containing the book data.</param>
	/// <param name="filePath">The original file path.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	async Task SaveBookToLibraryAsync(Book ebook, Stream stream, string filePath, CancellationToken cancellationToken)
	{
		try
		{
			ebook.FilePath = await FileService.SaveFileAsync(stream, filePath, cancellationToken).ConfigureAwait(false);
			ebook.CoverImagePath = await FileService.SaveImageAsync(filePath, ebook.CoverImage, cancellationToken).ConfigureAwait(false);

			if (ValidateBookFiles(ebook))
			{
				await db.SaveBookData(ebook, cancellationToken).ConfigureAwait(false);
				Books.Add(ebook);
				logger.Info($"Book saved successfully: {ebook.Title}");
			}
			else
			{
				await ShowErrorToastAsync($"Failed to save book: {ebook.Title}", cancellationToken);
			}
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving book from stream: {ex.Message}");
			await ShowErrorToastAsync("Error saving book", cancellationToken);
		}
	}

	/// <summary>
	/// Validates that the book files were saved successfully.
	/// </summary>
	/// <param name="ebook">The book to validate.</param>
	/// <returns>True if both the book file and cover image exist, false otherwise.</returns>
	static bool ValidateBookFiles(Book ebook)
	{
		return File.Exists(ebook.FilePath) && File.Exists(ebook.CoverImagePath);
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
	/// Asynchronously presents a file picker dialog to the user and returns the selected file.
	/// </summary>
	/// <param name="options">The options for picking files.</param>
	/// <returns>The selected file, or null if no file was selected or an error occurred.</returns>
	static async Task<FileResult?> PickAndShowAsync(PickOptions options, CancellationToken cancellationToken = default)
	{
		try
		{
			return await FilePicker.PickAsync(options).WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.Error($"Exception choosing file: {ex.Message}");
			return null;
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
