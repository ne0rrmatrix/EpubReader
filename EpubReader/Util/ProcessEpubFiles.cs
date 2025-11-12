namespace EpubReader.Util;

/// <summary>
/// Provides functionality to process EPUB files, including selecting, validating, and saving them to a library.
/// </summary>
/// <remarks>This class is responsible for handling EPUB files by allowing users to select files, process them
/// asynchronously, and save them to a library. It supports operations such as checking if a book is already in the
/// library and validating the saved files. The class uses platform-specific file type configurations for EPUB files and
/// integrates with services for file operations and messaging.</remarks>
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
					await ShowErrorToastAsync($"Error opening book: {Path.GetFileName(filePath)}");
					return;
				}

				if (await IsBookAlreadyInLibrary(ebook))
				{
					await ShowInfoToastAsync($"Book already exists in library: {ebook.Title}");
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
			await ShowErrorToastAsync($"Error processing {Path.GetFileName(filePath)}");
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
				await ShowErrorToastAsync($"Failed to save book: {ebook.Title}");
				return;
			}
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving book from stream: {ex.Message}");
			await ShowErrorToastAsync("Error saving book");
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
				await ShowInfoToastAsync("Book added to library");
				logger.Info($"Book added to library: {ebook.Title}");
			}
			else
			{
				await ShowErrorToastAsync($"Failed to save book: {ebook.Title}");
			}
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving book to library: {ex.Message}");
			await ShowErrorToastAsync("Error saving book to library");
		}
	}
	
	#endregion
	
	/// <summary>
	/// Downloads and processes an EPUB file for a given book, saving it to the cache directory and adding it to the
	/// library if not already present.
	/// </summary>
	/// <remarks>This method downloads the EPUB file from the specified URL, saves it to the cache directory, and
	/// attempts to add it to the library. If the book already exists in the library, it will not be added again. Errors
	/// during processing will be logged and a user notification will be shown.</remarks>
	/// <param name="book">The book object containing metadata and download URL information.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the file is successfully processed and added to the library; otherwise, <see
	/// langword="false"/>.</returns>
	public async Task<bool> ProcessFileAsync(Book book, CancellationToken cancellationToken)
	{
		using var httpClient = new HttpClient();
		using var memoryStream = new MemoryStream();
		try
		{
			using var stream = await httpClient.GetStreamAsync(book.DownloadUrl, cancellationToken);
			await stream.CopyToAsync(memoryStream, cancellationToken);
			memoryStream.Seek(0, SeekOrigin.Begin);

			var cacheDirectory = FileSystem.Current.CacheDirectory;
			var invalidPathChars = Path.GetInvalidFileNameChars();
			var extraInvalidChars = new char[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '(', ')', '#', '!', '@', '$', '%', '^', '-', '=', '_', '+'};
			var emptySpaces = " ";
			invalidPathChars = [.. invalidPathChars, .. emptySpaces];
			invalidPathChars = [.. invalidPathChars, .. extraInvalidChars];
			book.Title = string.Concat(book.Title.Split(invalidPathChars, StringSplitOptions.RemoveEmptyEntries));
			book.FilePath = Path.Combine(cacheDirectory, $"{book.Title}.epub");
			var fileBytes = memoryStream.ToArray();
			await File.WriteAllBytesAsync(book.FilePath, fileBytes, cancellationToken);
			logger.Info($"File saved: {book.FilePath}");
		}
		catch (Exception ex)
		{
			logger.Error($"Error processing file: {ex.Message}");
			if(ex.StackTrace is not null) { logger.Error(ex.StackTrace); }
			return false;
		}
	

		try
		{

			var ebook = await EbookService.GetListingAsync(book.FilePath);
			if (ebook is null)
			{
				logger.Error("Error opening book after download.");
				return false;
			}

			if (await IsBookAlreadyInLibrary(ebook))
			{
				logger.Info("Book already exists in library.");
				return false;
			}
			memoryStream.Seek(0, SeekOrigin.Begin);
			await SaveBookToLibraryAsync(ebook, memoryStream, book.FilePath, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.Error($"Error adding book: {ex.Message}");
			if(ex.StackTrace is not null) {
				logger.Error(ex.StackTrace);
			}
			return false;
		}
		return true;
	}
}
