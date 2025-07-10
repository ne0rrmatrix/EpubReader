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
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FileService));
	public static readonly string SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpubReader");

	/// <summary>
	/// Deletes the specified directory and its contents.
	/// </summary>
	/// <remarks>If the directory exists, it and all its contents are deleted. If the directory does not exist, no
	/// action is taken. Logs an informational message upon successful deletion and an error message if an exception occurs
	/// during the process.</remarks>
	/// <param name="directoryName">The path of the directory to delete. Must not be null or empty.</param>
	public static void DeleteDirectory(string directoryName)
	{
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
			logger.Error($"Error deleting directory: {directoryName}, Messsage: {ex.Message}");
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
			logger.Error($"Error deleting file: {fileName}, Messsage: {ex.Message}");
		}
	}

	/// <summary>
	/// Asynchronously saves an image to a directory based on the provided book name.
	/// </summary>
	/// <remarks>The method creates a directory based on the book name if it does not already exist. The image is
	/// saved with a file extension determined by the image type.</remarks>
	/// <param name="bookName">The name of the book, used to determine the directory and file name for saving the image.</param>
	/// <param name="imageBytes">The byte array representing the image to be saved.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the full path of the saved image file,
	/// or an empty string if the operation fails.</returns>
	public static async Task<string> SaveImageAsync(string bookName, byte[] imageBytes)
	{
		var partialPath = Path.GetFileNameWithoutExtension(bookName);
		var fullPath = Path.Combine(SaveDirectory, ValidateAndFixDirectoryName(partialPath));
		if (!Directory.Exists(fullPath))
		{
			Directory.CreateDirectory(fullPath);
			logger.Info($"Created directory: {fullPath}");
		}
		
		string fileName;
		
		try
		{
			using var memoryStream = new MemoryStream(imageBytes);
			var type = ImageExtensions.GetFileExtension(memoryStream);
			var newfileName = Path.ChangeExtension(bookName, type.ToString().ToLower());
			fileName = Path.Combine(fullPath, ValidateAndFixFileName(newfileName));
			
			memoryStream.Seek(0, SeekOrigin.Begin);
			
			// Simplified file writing with less chances of handle leaks
			await File.WriteAllBytesAsync(fileName, memoryStream.ToArray());
			logger.Info($"Image saved: {bookName}");
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving image: {bookName}, Message: {ex.Message}");
			return string.Empty;
		}
		
		return fileName;
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
	public static async Task<string> SaveFileAsync(Stream stream, string bookName)
	{
		try
		{
			var fullPath = Path.Combine(SaveDirectory, ValidateAndFixDirectoryName(Path.GetFileNameWithoutExtension(bookName)));
			if (!Directory.Exists(fullPath))
			{
				Directory.CreateDirectory(fullPath);
				logger.Info($"Created directory: {fullPath}");
			}
			
			var fileName = Path.Combine(fullPath, ValidateAndFixDirectoryName(Path.GetFileNameWithoutExtension(bookName)));
			fileName = Path.ChangeExtension(fileName, ".epub");
			
			// Create a memory buffer to ensure we're not locking the original stream
			using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(memoryStream);
			memoryStream.Position = 0;
			
			// Write to file in one operation
			await File.WriteAllBytesAsync(fileName, memoryStream.ToArray());
			
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
	public static async Task<string> SaveFileAsync(FileResult result, string bookName)
	{
		try
		{
			bookName = Path.GetFileNameWithoutExtension(bookName);
			var fullPath = Path.Combine(SaveDirectory, ValidateAndFixDirectoryName(bookName));
			if (!Directory.Exists(fullPath))
			{
				Directory.CreateDirectory(fullPath);
				logger.Info($"Created directory: {fullPath}");
			}

			var fileName = Path.Combine(fullPath, ValidateAndFixFileName(result.FileName));
			
			// Use a memory buffer to avoid file handle issues
			using (Stream fileStream = await result.OpenReadAsync())
			{
				using var memoryStream = new MemoryStream();
				await fileStream.CopyToAsync(memoryStream);
				memoryStream.Position = 0;
				
				// Write the bytes in one operation
				await File.WriteAllBytesAsync(fileName, memoryStream.ToArray());
			}
			
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
	/// Determines the MIME type based on the file extension of the specified file name.
	/// </summary>
	/// <remarks>This method uses a predefined mapping of common file extensions to their respective MIME
	/// types.</remarks>
	/// <param name="fileName">The name of the file whose MIME type is to be determined. The file name must include an extension.</param>
	/// <returns>A string representing the MIME type corresponding to the file extension.  If the extension is not recognized,
	/// returns <see langword="application/octet-stream"/>.</returns>
	public static string GetMimeType(string fileName)
	{
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

	static string ValidateAndFixDirectoryName(string directoryName)
	{
		char[] invalidChars = Path.GetInvalidPathChars();
		foreach (char invalidChar in invalidChars)
		{
			directoryName = directoryName.Replace(invalidChar, '_');
		}

		directoryName = directoryName.Replace("\\", "");
		directoryName = directoryName.Replace("/", "");
		directoryName = directoryName.Replace(":", "");
		directoryName = directoryName.Replace("*", "");
		directoryName = directoryName.Replace("?", "");
		directoryName = directoryName.Replace("\"", "");
		directoryName = directoryName.Replace("<", "");
		directoryName = directoryName.Replace(">", "");
		directoryName = directoryName.Replace("|", "");
		directoryName = directoryName.Replace("$", "");
		directoryName = directoryName.Replace("%", "");
		directoryName = directoryName.Replace(" ", "").Trim();
		directoryName = Path.GetFileNameWithoutExtension(directoryName);
		return directoryName;
	}

	static string ValidateAndFixFileName(string fileName)
	{
		char[] invalidChars = Path.GetInvalidFileNameChars();
		foreach (char invalidChar in invalidChars)
		{
			fileName = fileName.Replace(invalidChar, '_');
		}
		fileName = fileName.Replace(" ", "").Trim();
		return fileName;
	}

}