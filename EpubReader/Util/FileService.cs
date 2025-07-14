using MetroLog;

namespace EpubReader.Util;

/// <summary>
/// Provides static methods for file and directory operations, including deletion and saving of files and images.
/// </summary>
/// <remarks>The <see cref="FileService"/> class offers utility methods to manage files and directories, such as
/// deleting directories and files, and saving images and files asynchronously. It logs informational and error messages
/// during operations to assist with monitoring and debugging. The class is designed to work with a specific save
/// directory, which is determined by the application's local data folder.</remarks>
public static partial class FileService
{
	#region Constants and Static Fields

	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FileService));
	public static readonly string SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpubReader");

	// Cache for invalid characters to avoid repeated allocations
	static readonly char[] invalidPathChars = Path.GetInvalidPathChars();
	static readonly char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
	static readonly char[] additionalInvalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|', '$', '%'];

	#endregion

	#region File and Directory Deletion

	/// <summary>
	/// Deletes the specified directory and its contents.
	/// </summary>
	/// <remarks>If the directory exists, it and all its contents are deleted. If the directory does not exist, no
	/// action is taken. Logs an informational message upon successful deletion and an error message if an exception occurs
	/// during the process.</remarks>
	/// <param name="directoryName">The path of the directory to delete. Must not be null or empty.</param>
	public static void DeleteDirectory(string directoryName)
	{
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			logger.Warn("Directory name is null or empty");
			return;
		}

		try
		{
			if (Directory.Exists(directoryName))
			{
				Directory.Delete(directoryName, true);
				logger.Info($"Deleted directory {directoryName}");
			}
		}
		catch (Exception ex)
		{
			logger.Error($"Error deleting directory: {directoryName}, Message: {ex.Message}");
		}
	}

	/// <summary>
	/// Deletes the specified file if it exists.
	/// </summary>
	/// <remarks>If the file exists, it is deleted and an informational log entry is created.  If an error occurs
	/// during deletion, an error log entry is created.</remarks>
	/// <param name="fileName">The path of the file to be deleted. Cannot be null or empty.</param>
	public static void DeleteFile(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			logger.Warn("File name is null or empty");
			return;
		}

		try
		{
			if (File.Exists(fileName))
			{
				File.Delete(fileName);
				logger.Info($"Deleted file {fileName}");
			}
		}
		catch (Exception ex)
		{
			logger.Error($"Error deleting file: {fileName}, Message: {ex.Message}");
		}
	}

	#endregion

	#region File Saving Operations

	/// <summary>
	/// Asynchronously saves an image to a directory based on the provided book name.
	/// </summary>
	/// <remarks>The method creates a directory based on the book name if it does not already exist. The image is
	/// saved with a file extension determined by the image type.</remarks>
	/// <param name="bookName">The name of the book, used to determine the directory and file name for saving the image.</param>
	/// <param name="imageBytes">The byte array representing the image to be saved.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the full path of the saved image file,
	/// or an empty string if the operation fails.</returns>
	public static async Task<string> SaveImageAsync(string bookName, byte[] imageBytes, CancellationToken cancellation = default)
	{
		if (string.IsNullOrWhiteSpace(bookName))
		{
			logger.Warn("Book name is null or empty");
			return string.Empty;
		}

		if (imageBytes == null || imageBytes.Length == 0)
		{
			logger.Warn("Image bytes are null or empty");
			return string.Empty;
		}

		try
		{
			var directoryPath = CreateBookDirectoryAsync(bookName);
			if (string.IsNullOrEmpty(directoryPath))
			{
				return string.Empty;
			}

			var fileName = GenerateImageFileName(imageBytes, bookName, directoryPath);
			if (string.IsNullOrEmpty(fileName))
			{
				return string.Empty;
			}

			await File.WriteAllBytesAsync(fileName, imageBytes, cancellation);
			logger.Info($"Image saved: {bookName}");
			return fileName;
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving image: {bookName}, Message: {ex.Message}");
			return string.Empty;
		}
	}

	/// <summary>
	/// Asynchronously saves a stream to a file with the specified book name in the designated directory.
	/// </summary>
	/// <remarks>The method creates a directory based on the book name if it does not already exist. The file is
	/// saved with an ".epub" extension.</remarks>
	/// <param name="stream">The input stream containing the data to be saved. The stream must be readable and will be copied to a memory
	/// buffer.</param>
	/// <param name="bookName">The name of the book, used to determine the file name and directory. The name should not include an extension.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the full path of the saved file if
	/// successful; otherwise, an empty string.</returns>
	public static async Task<string> SaveFileAsync(Stream stream, string bookName, CancellationToken cancellation = default)
	{
		if (stream == null)
		{
			logger.Warn("Stream is null");
			return string.Empty;
		}

		if (string.IsNullOrWhiteSpace(bookName))
		{
			logger.Warn("Book name is null or empty");
			return string.Empty;
		}

		try
		{
			var directoryPath = CreateBookDirectoryAsync(bookName);
			if (string.IsNullOrEmpty(directoryPath))
			{
				return string.Empty;
			}

			var fileName = GenerateEpubFileName(bookName, directoryPath);
			var fileBytes = await ReadStreamToBytesAsync(stream, cancellation);

			await File.WriteAllBytesAsync(fileName, fileBytes, cancellation);
			logger.Info($"File saved: {fileName}");
			return fileName;
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving file: {ex.Message}");
			return string.Empty;
		}
	}

	/// <summary>
	/// Asynchronously saves a file to a specified directory based on the provided book name.
	/// </summary>
	/// <remarks>The method creates a directory named after the book if it does not already exist, and saves the
	/// file within this directory. The file name is validated and adjusted to ensure it is suitable for the file
	/// system.</remarks>
	/// <param name="result">The file result containing the file to be saved.</param>
	/// <param name="bookName">The name of the book used to determine the directory path. The directory will be created if it does not exist.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the full path of the saved file, or an
	/// empty string if an error occurs.</returns>
	public static async Task<string> SaveFileAsync(FileResult result, string bookName, CancellationToken cancellationToken = default)
	{
		if (result == null)
		{
			logger.Warn("FileResult is null");
			return string.Empty;
		}

		if (string.IsNullOrWhiteSpace(bookName))
		{
			logger.Warn("Book name is null or empty");
			return string.Empty;
		}

		try
		{
			var directoryPath = CreateBookDirectoryAsync(bookName);
			if (string.IsNullOrEmpty(directoryPath))
			{
				return string.Empty;
			}

			var fileName = Path.Combine(directoryPath, ValidateAndFixFileName(result.FileName));

			using var fileStream = await result.OpenReadAsync();
			var fileBytes = await ReadStreamToBytesAsync(fileStream, cancellationToken);

			await File.WriteAllBytesAsync(fileName, fileBytes, cancellationToken);
			logger.Info($"File saved: {fileName}");
			return fileName;
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving file: {ex.Message}");
			return string.Empty;
		}
	}

	#endregion

	#region MIME Type Detection

	/// <summary>
	/// Determines the MIME type based on the file extension of the specified file name.
	/// </summary>
	/// <remarks>This method uses a predefined mapping of common file extensions to their respective MIME
	/// types.</remarks>
	/// <param name="fileName">The name of the file whose MIME type is to be determined. The file name must include an extension.</param>
	/// <returns>A string representing the MIME type corresponding to the file extension.  If the extension is not recognized,
	/// returns <see langword="application/octet-stream"/>.</returns>
	public static string GetMimeType(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return "application/octet-stream";
		}

		var extension = Path.GetExtension(fileName).ToLowerInvariant();

		return extension switch
		{
			".xhtml" => "application/xhtml+xml",
			".html" or ".htm" => "text/html",
			".css" => "text/css",
			".ico" => "image/x-icon",
			".js" => "application/javascript",
			".json" => "application/json",
			".png" => "image/png",
			".jpg" or ".jpeg" => "image/jpeg",
			".gif" => "image/gif",
			".svg" => "image/svg+xml",
			".pdf" => "application/pdf",
			".txt" => "text/plain",
			_ => "application/octet-stream"
		};
	}

	#endregion

	#region Private Helper Methods

	/// <summary>
	/// Creates a directory for the specified book name if it doesn't exist.
	/// </summary>
	/// <param name="bookName">The name of the book to create directory for.</param>
	/// <returns>The full path of the created directory, or empty string if failed.</returns>
	static string CreateBookDirectoryAsync(string bookName)
	{
		var sanitizedBookName = ValidateAndFixDirectoryName(Path.GetFileNameWithoutExtension(bookName));
		var directoryPath = Path.Combine(SaveDirectory, sanitizedBookName);

		try
		{
			if (!Directory.Exists(directoryPath))
			{
				Directory.CreateDirectory(directoryPath);
				logger.Info($"Created directory: {directoryPath}");
			}
			return directoryPath;
		}
		catch (Exception ex)
		{
			logger.Error($"Error creating directory: {directoryPath}, Message: {ex.Message}");
			return string.Empty;
		}
	}

	/// <summary>
	/// Generates a file name for an image based on its content and the book name.
	/// </summary>
	/// <param name="imageBytes">The image bytes to determine file extension.</param>
	/// <param name="bookName">The name of the book.</param>
	/// <param name="directoryPath">The directory path where the file will be saved.</param>
	/// <returns>The full file path with appropriate extension.</returns>
	static string GenerateImageFileName(byte[] imageBytes, string bookName, string directoryPath)
	{
		try
		{
			using var memoryStream = new MemoryStream(imageBytes);
			var fileExtension = ImageExtensions.GetFileExtension(memoryStream);

			if (string.IsNullOrEmpty(fileExtension))
			{
				logger.Warn($"Could not determine image type for {bookName}");
				return string.Empty;
			}

			var baseFileName = Path.GetFileNameWithoutExtension(bookName);
			var fileName = Path.ChangeExtension(baseFileName, fileExtension.ToLowerInvariant());
			var sanitizedFileName = ValidateAndFixFileName(fileName);

			return Path.Combine(directoryPath, sanitizedFileName);
		}
		catch (Exception ex)
		{
			logger.Error($"Error generating image file name: {ex.Message}");
			return string.Empty;
		}
	}

	/// <summary>
	/// Generates a file name for an EPUB file.
	/// </summary>
	/// <param name="bookName">The name of the book.</param>
	/// <param name="directoryPath">The directory path where the file will be saved.</param>
	/// <returns>The full file path with .epub extension.</returns>
	static string GenerateEpubFileName(string bookName, string directoryPath)
	{
		var baseFileName = Path.GetFileNameWithoutExtension(bookName);
		var sanitizedFileName = ValidateAndFixFileName(baseFileName);
		var fileName = Path.ChangeExtension(sanitizedFileName, ".epub");

		return Path.Combine(directoryPath, fileName);
	}

	/// <summary>
	/// Reads a stream to a byte array asynchronously.
	/// </summary>
	/// <param name="stream">The stream to read from.</param>
	/// <returns>A byte array containing the stream content.</returns>
	static async Task<byte[]> ReadStreamToBytesAsync(Stream stream, CancellationToken cancellation = default)
	{
		if (stream.CanSeek)
		{
			stream.Position = 0;
		}

		using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream, cancellation);
		return memoryStream.ToArray();
	}

	/// <summary>
	/// Validates and fixes a directory name by removing invalid characters.
	/// </summary>
	/// <param name="directoryName">The directory name to validate and fix.</param>
	/// <returns>A sanitized directory name safe for file system use.</returns>
	static string ValidateAndFixDirectoryName(string directoryName)
	{
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			return "Unknown";
		}

		// Remove invalid path characters
		foreach (var invalidChar in invalidPathChars)
		{
			directoryName = directoryName.Replace(invalidChar, '_');
		}

		// Remove additional problematic characters
		foreach (var invalidChar in additionalInvalidChars)
		{
			directoryName = directoryName.Replace(invalidChar, '_');
		}

		// Remove spaces and trim
		directoryName = directoryName.Replace(" ", "").Trim();

		// Ensure we have a valid directory name
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			return "Unknown";
		}

		return directoryName;
	}

	/// <summary>
	/// Validates and fixes a file name by removing invalid characters.
	/// </summary>
	/// <param name="fileName">The file name to validate and fix.</param>
	/// <returns>A sanitized file name safe for file system use.</returns>
	static string ValidateAndFixFileName(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return "unknown";
		}

		// Remove invalid file name characters
		foreach (var invalidChar in invalidFileNameChars)
		{
			fileName = fileName.Replace(invalidChar, '_');
		}

		// Remove spaces and trim
		fileName = fileName.Replace(" ", "").Trim();

		// Ensure we have a valid file name
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return "unknown";
		}

		return fileName;
	}

	#endregion
}