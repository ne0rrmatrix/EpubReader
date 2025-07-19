using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Util;
public partial class ProcessEpubFiles : BaseViewModel
{
	static readonly string[] epubExtensions = [".epub"];
	static readonly string[] androidEpubTypes = ["application/epub+zip", ".epub"];
	static readonly string[] iOSEpubTypes = ["org.idpf.epub-container"];

	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(ProcessEpubFiles));
	public readonly IFolderPicker FolderPicker;
	public readonly FilePickerFileType CustomFileType;
	public ProcessEpubFiles()
	{
		FolderPicker = GetFolderPickerService();
		CustomFileType = CreateCustomFileType();
	}

	/// <summary>
	/// Processes a collection of EPUB files from a folder.
	/// </summary>
	/// <param name="epubFiles">The list of EPUB file paths to process.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task<int> ProcessEpubFilesAsync(List<string> epubFiles, CancellationToken cancellationToken)
	{
		
		int count = 0;
		foreach (var file in epubFiles)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			count++;
			await ProcessSingleEpubFileAsync(file, epubFiles.Count, count, cancellationToken).ConfigureAwait(false);
			
		}

		return count;
	}
	
	/// <summary>
	/// Processes a single EPUB file from a folder operation.
	/// </summary>
	/// <param name="filePath">The path to the EPUB file.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	async Task ProcessSingleEpubFileAsync(string filePath, int maxCount, int count, CancellationToken cancellationToken)
	{
		try
		{
			var stream = await FolderPicker.PerformFileOperationOnEpubAsync(filePath, cancellationToken).ConfigureAwait(false);
			if (stream is null)
			{
				logger.Info($"Failed to open stream for file: {filePath}");
				return;
			}

			using (stream)
			{
				if(cancellationToken.IsCancellationRequested)
				{
					logger.Info("Operation cancelled by user.");
					return;
				}
				var ebook = await EbookService.GetListingAsync(stream, filePath).ConfigureAwait(false);
				if (ebook is null)
				{
					await ShowErrorToastAsync($"Error opening book: {Path.GetFileName(filePath)}", cancellationToken);
					return;
				}

				if (await IsBookAlreadyInLibrary(ebook))
				{
					await ShowInfoToastAsync($"Book already exists in library: {ebook.Title}", cancellationToken);
					return;
				}
				WeakReferenceMessenger.Default.Send(new FolderMessage(new FolderInfo
				{
					Title = ebook.Title,
					MaxCount = maxCount,
					Count = count,
				}));
				logger.Info($"Processing file {Path.GetFileName(filePath)} ({count}/{maxCount})");
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
			ebook.IsInLibrary = true; // Ensure the book is marked as in library
			if (ValidateBookFiles(ebook))
			{
				await db.SaveBookData(ebook, cancellationToken).ConfigureAwait(false);
				WeakReferenceMessenger.Default.Send(new BookMessage(ebook));
				logger.Info($"Book saved successfully: {ebook.Title}");
				return;
			}
			else
			{
				await ShowErrorToastAsync($"Failed to save book: {ebook.Title}", cancellationToken);
				return;
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
	/// Asynchronously presents a file picker dialog to the user and returns the selected file.
	/// </summary>
	/// <param name="options">The options for picking files.</param>
	/// <returns>The selected file, or null if no file was selected or an error occurred.</returns>
	static async Task<FileResult?> PickAndShowAsync(PickOptions options, CancellationToken cancellationToken)
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
	public async Task<FileResult?> PickEpubFileAsync(CancellationToken cancellationToken = default)
	{
		var options = new PickOptions
		{
			FileTypes = CustomFileType,
			PickerTitle = "Please select an EPUB book"
		};

		return await PickAndShowAsync(options, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Checks if a book is already in the library based on its title.
	/// </summary>
	/// <param name="ebook">The book to check.</param>
	/// <returns>True if the book already exists in the library, false otherwise.</returns>
	public async Task<bool> IsBookAlreadyInLibrary(Book ebook)
	{
		var books = await db.GetAllBooks();
		return books.Any(x => string.Equals(x.Title, ebook.Title, StringComparison.OrdinalIgnoreCase));
	}
	
	/// <summary>
	/// Saves a book to the library from a FileResult.
	/// </summary>
	/// <param name="ebook">The book to save.</param>
	/// <param name="fileResult">The file result containing the book data.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task SaveBookToLibraryAsync(Book ebook, FileResult fileResult, CancellationToken cancellationToken)
	{
		try
		{
			ebook.FilePath = await FileService.SaveFileAsync(fileResult, ebook.FilePath, cancellationToken).ConfigureAwait(false);
			ebook.CoverImagePath = await FileService.SaveImageAsync(ebook.FilePath, ebook.CoverImage, cancellationToken).ConfigureAwait(false);

			if (ValidateBookFiles(ebook))
			{
				await db.SaveBookData(ebook, cancellationToken).ConfigureAwait(false);
				WeakReferenceMessenger.Default.Send(new BookMessage(ebook));
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
		if(cancellationToken.IsCancellationRequested)
		{
			return;
		}
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
		if(cancellationToken.IsCancellationRequested)
		{
			return;
		}
		await Dispatcher.DispatchAsync(async () =>
			await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
		logger.Error(message);
	}
	
	public async Task ProcessFileAsync(Book book, CancellationToken cancellationToken)
	{
		using var httpClient = new HttpClient();
		using var stream = await httpClient.GetStreamAsync(book.DownloadUrl, cancellationToken);
		
		var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream, cancellationToken);
		memoryStream.Seek(0, SeekOrigin.Begin);
		
		var cacheDirectory = FileSystem.Current.CacheDirectory;
		var invalidPathChars = Path.GetInvalidFileNameChars();
		book.Title = string.Concat(book.Title.Split(invalidPathChars, StringSplitOptions.RemoveEmptyEntries));
		book.FilePath = Path.Combine(cacheDirectory, $"{book.Title}.epub");

		var fileBytes = memoryStream.ToArray();
		await File.WriteAllBytesAsync(book.FilePath, fileBytes, cancellationToken).ConfigureAwait(false);
		logger.Info($"File saved: {book.FilePath}");

		try
		{

			var ebook = await EbookService.GetListingAsync(book.FilePath).ConfigureAwait(false);
			if (ebook is null)
			{
				await ShowErrorToastAsync("Error opening book. Please select a valid EPUB file.", cancellationToken);
				return;
			}

			if (await IsBookAlreadyInLibrary(ebook))
			{
				await ShowInfoToastAsync($"Book already exists in library: {ebook.Title}", cancellationToken);
				return;
			}
			memoryStream.Seek(0, SeekOrigin.Begin);
			await SaveBookToLibraryAsync(ebook, memoryStream, book.FilePath, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.Error($"Error adding book: {ex.Message}");
			await ShowErrorToastAsync("Error adding book. Please try again.", cancellationToken);
		}
	}
	
	#endregion
}
