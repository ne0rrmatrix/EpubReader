using MetroLog;
using static SQLite.SQLite3;

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
		}
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
		}
		using var memoryStream = new MemoryStream(imageBytes);
		IsImageExtension.IsImage(memoryStream, out var type);

	     var newfileName = Path.ChangeExtension(bookName, type.ToString().ToLower());
		var fileName = Path.Combine(fullPath, ValidateAndFixFileName(newfileName));
		await File.WriteAllBytesAsync(fileName, imageBytes);
		logger.Info($"Image saved: {bookName}");
		return fileName;
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
			}

			using Stream fileStream = await result.OpenReadAsync();
			using StreamReader reader = new(fileStream);
			var fileName = Path.Combine(fullPath, ValidateAndFixFileName(result.FileName));
			using FileStream output = File.Create(fileName);
			await fileStream.CopyToAsync(output);
			fileStream.Seek(0, SeekOrigin.Begin);
			Stream.Synchronized(output);
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