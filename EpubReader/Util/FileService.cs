using MetroLog;

namespace EpubReader.Util;

public static partial class FileService
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FileService));
	public static readonly string SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpubReader");

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

	public static async Task<string> SaveImage(string bookName, byte[] imageBytes)
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
	
	public static async Task<string> SaveFile(Stream stream, string bookName)
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
	
	public static async Task<string> SaveFile(FileResult result, string bookName)
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
}