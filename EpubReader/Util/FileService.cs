﻿using MetroLog;

namespace EpubReader.Util;

public static partial class FileService
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FileService));
	public static readonly string SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpubReader");
	public static readonly string WWWDirectory = Path.Combine(SaveDirectory, "wwwroot");

	public static string ValidateAndFixDirectoryName(string directoryName)
	{
		char[] invalidChars = Path.GetInvalidPathChars();
		foreach (char invalidChar in invalidChars)
		{
			directoryName = directoryName.Replace(invalidChar, '_');
		}
		directoryName = directoryName.Replace(" ", "").Trim();
		return directoryName;
	}

	public static string ValidateAndFixFileName(string fileName)
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

	public static string GetFileName(string name)
	{
		var filename = Path.GetFileName(name);
		return Path.Combine(SaveDirectory, filename);
	}

	public static async Task SaveFile(FileResult result)
	{
		try
		{
			if (Directory.Exists(SaveDirectory))
			{
				logger.Info("Directory exists");
			}
			else
			{
				logger.Info("Directory does not exist");
				Directory.CreateDirectory(SaveDirectory);
			}

			using Stream fileStream = await result.OpenReadAsync();
			using StreamReader reader = new(fileStream);
			var fileName = GetFileName(result.FileName);
			using FileStream output = File.Create(fileName);
			await fileStream.CopyToAsync(output);
			fileStream.Seek(0, SeekOrigin.Begin);
			Stream.Synchronized(output);
			logger.Info($"File saved: {fileName}");
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving file: {ex.Message}");
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