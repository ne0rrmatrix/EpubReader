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
/// Represents the view model for managing a library of books, providing functionality to add, remove, and navigate
/// books.
/// </summary>
/// <remarks>The <see cref="LibraryViewModel"/> class is responsible for handling operations related to a
/// collection of books, including adding books from files, removing books, and navigating to a book's page. It
/// interacts with services for file picking, database operations, and ebook processing. This class is designed to be
/// used in a UI context where users can manage their book library.</remarks>
public partial class LibraryViewModel : BaseViewModel
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(LibraryViewModel));
	readonly IFolderPicker folderPicker = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IFolderPicker>() ?? throw new InvalidOperationException();
	static readonly string[] epub = [".epub", ".epub"];
    static readonly string[] android_epub = ["application/epub+zip", ".epub"];
	readonly FilePickerFileType customFileType = new(
		  new Dictionary<DevicePlatform, IEnumerable<string>>
		  {
			 { DevicePlatform.iOS, new[] { "org.idpf.epub-container" } },
			 { DevicePlatform.MacCatalyst, new[] { "org.idpf.epub-container" } },
			 { DevicePlatform.Android, android_epub },
			 { DevicePlatform.WinUI, epub },
			 { DevicePlatform.Tizen, epub },
		  });

	/// <summary>
	/// Gets or sets the collection of books.
	/// </summary>
	[ObservableProperty]
    public partial ObservableCollection<Book> Books { get; set; }
   
	/// <summary>
	/// Initializes a new instance of the <see cref="LibraryViewModel"/> class.
	/// </summary>
	/// <remarks>This constructor initializes the <see cref="Books"/> collection with all available books retrieved
	/// from the database. If no books are found, the collection is initialized as empty.</remarks>
	public LibraryViewModel()
    {
		Books = [.. db.GetAllBooks() ?? []];
	}

	/// <summary>
	/// Navigates to the book page asynchronously, opening the specified book and setting its current state.
	/// </summary>
	/// <param name="book">The book to open and navigate to. Must not be <see langword="null"/>.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	/// <exception cref="InvalidOperationException">Thrown if there is an error opening the ebook.</exception>
    [RelayCommand]
    public async Task GotoBookPageAsync(Book book)
    {
		var temp = db.GetBook(book);
		ArgumentNullException.ThrowIfNull(temp);
		Book = await EbookService.OpenEbookAsync(book.FilePath).ConfigureAwait(true) ?? throw new InvalidOperationException("Error opening ebook");
		Book.CurrentChapter = temp.CurrentChapter;
		Book.Id = temp.Id;
		StreamExtensions.Instance?.SetBook(Book);
		var navigationParams = new Dictionary<string, object>
        {
            { "Book", Book }
        };
        await Shell.Current.GoToAsync($"BookPage", navigationParams);
    }

	/// <summary>
	/// Asynchronously adds EPUB files from a selected folder to the library.
	/// </summary>
	/// <remarks>This method allows the user to select a folder and processes all EPUB files within it. If no folder
	/// is selected or no EPUB files are found, appropriate messages are logged and displayed. Each EPUB file is processed
	/// to extract book information, which is then saved to the library if it does not already exist.</remarks>
	/// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
	/// <returns></returns>
	[RelayCommand]
	async Task AddFolderAsync(CancellationToken cancellationToken = default)
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
			logger.Info("No epub files found in the selected folder");
			await Dispatcher.DispatchAsync(async () => await Toast.Make("No epub files found in the selected folder", ToastDuration.Short, 12).Show(cancellationToken));
			return;
		}
		logger.Info($"Found {epubFiles.Count} epub files in the selected folder");
		string message = string.Empty;

		
		foreach (var file in epubFiles)
		{
			var stream = await folderPicker.PerformFileOperationOnEpubAsync(file);
			if (stream is null)
			{
				logger.Info($"Failed to open stream for file: {file}");
				continue;
			}
			Book? ebook = null;
			ebook = EbookService.GetListing(stream, file);
			stream.Seek(0, SeekOrigin.Begin); // Reset stream position for reading
			if (ebook is null)
			{
				message = $"Error opening Book: {file}";
				await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
				logger.Info(message);
				continue;
			}
			if (Books.Any(x => x.Title == ebook.Title))
			{
				message = $"Book already exists in library: {ebook.Title}";
				await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
				logger.Info(message);
				continue;
			}
			
			ebook.FilePath = await FileService.SaveFileAsync(stream, file);
			ebook.CoverImagePath = await FileService.SaveImageAsync(file, ebook.CoverImage);
			if (File.Exists(ebook.FilePath) && File.Exists(ebook.CoverImagePath))
			{
				logger.Info($"Book {ebook.Title} saved successfully.");
			}
			else
			{
				logger.Error($"Failed to save book {ebook.Title} or its cover image.");
				message = $"Failed to save book {ebook.Title} or its cover image.";
				await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
				continue;
			}

			db.SaveBookData(ebook);
			Books.Add(ebook);
		}
	}

	/// <summary>
	/// Asynchronously adds a selected ePub book to the library.
	/// </summary>
	/// <remarks>This method prompts the user to select an ePub file, verifies its uniqueness in the library, and
	/// saves it to the database. If the book already exists, a notification is shown.</remarks>
	/// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
	/// <returns></returns>
	[RelayCommand]
    async Task AddAsync(CancellationToken cancellationToken = default)
    {
		string message = string.Empty;
        var result = await PickAndShowAsync(new PickOptions
        {
            FileTypes = customFileType,
            PickerTitle = "Please select a epub book"
        }).ConfigureAwait(false);
		if(result is null)
		{
			logger.Info("No file selected");
			return;
		}

		var ebook = EbookService.GetListing(result.FullPath);
		if (ebook is null)
		{
			message = "Error opening Book.";
			await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
			logger.Info(message);
			return;
		}
		if (Books.Any(x => x.Title == ebook.Title))
		{
			message = $"Book already exists in library: {ebook.Title}";
			await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
			logger.Info(message);
			return;
		}
		
		ebook.FilePath =  await FileService.SaveFileAsync(result, ebook.FilePath).ConfigureAwait(false);
		ebook.CoverImagePath = await FileService.SaveImageAsync(ebook.FilePath, ebook.CoverImage).ConfigureAwait(false);
		db.SaveBookData(ebook);
		Books.Add(ebook);

		message = "Book added to library";
		await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
		logger.Info(message);
	}

	/// <summary>
	/// Removes the specified book from the library.
	/// </summary>
	/// <remarks>This method deletes the directory containing the book's file and removes the book from the database
	/// and the in-memory collection.</remarks>
	/// <param name="book">The book to be removed. The book's file path must not be null.</param>
    [RelayCommand]
    void RemoveBook(Book book)
    {
		logger.Info($"Removing book {book.FilePath}");
		var directory = Path.GetDirectoryName(book.FilePath);
		if(directory is not null)
		{
			Directory.Delete(directory, true);
		}
		
		db.RemoveBook(book);
		Books.Remove(book);
		logger.Info("Book removed from library.");
		OnPropertyChanged(nameof(Books));
	}

	/// <summary>
	/// Asynchronously presents a file picker dialog to the user and returns the selected file.
	/// </summary>
	/// <param name="options">The options for picking files.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the selected file, or null if no file was selected.</returns>
	/// <exception cref="Exception">Thrown if an error occurs while picking the file.</exception>
	public static async Task<FileResult?> PickAndShowAsync(PickOptions options)
    {
        try
        {
            return await FilePicker.PickAsync(options).WaitAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.Error($"Exception choosing file: {ex.Message}");
            return null;
        }
    }

}
