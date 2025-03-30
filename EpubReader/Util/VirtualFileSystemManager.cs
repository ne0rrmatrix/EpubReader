using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace EpubReader.Util;

public static class ThreadSafeFileWriter
{

	public static string Path { get; set; } = string.Empty;
	public static string ReadFile(string fileName)
	{
		fileName = System.IO.Path.Combine(Path, fileName);

		// This block will be protected area
		using var mutex = new Mutex(false, fileName.Replace("\\", ""));
		var hasHandle = false;
		try
		{
			// Wait for the muted to be available
			hasHandle = mutex.WaitOne(Timeout.Infinite, false);
			// Do the file read
			if (!File.Exists(fileName))
			{
				return string.Empty;
			}

			return File.ReadAllText(fileName);
		}
		catch (Exception)
		{
			throw new InvalidOleVariantTypeException("Error reading file");
		}
		finally
		{
			// Very important! Release the mutex
			// Or the code will be locked forever
			if (hasHandle)
			{
				mutex.ReleaseMutex();
			}
		}
	}

	public static Stream ReadFileStream(string fileName)
	{
		fileName = System.IO.Path.Combine(Path, fileName);

		// This block will be protected area
		using var mutex = new Mutex(false, fileName.Replace("\\", ""));
		var hasHandle = false;
		try
		{
			// Wait for the muted to be available
			hasHandle = mutex.WaitOne(Timeout.Infinite, false);
			// Do the file read
			if (!File.Exists(fileName))
			{
				return Stream.Null;
			}
			SafeFileHandle safe = File.OpenHandle(fileName, FileMode.Open, FileAccess.Read);
			return new FileStream(safe, FileAccess.Read);
		}
		catch (Exception)
		{
			throw new InvalidOleVariantTypeException("Error reading file");
		}
		finally
		{
			// Very important! Release the mutex
			// Or the code will be locked forever
			if (hasHandle)
			{
				mutex.ReleaseMutex();
			}
		}
	}
	public static void WriteFile(string fileContents, string fileName)
	{
		fileName = System.IO.Path.Combine(Path, fileName);

		using var mutex = new Mutex(false, fileName.Replace("\\", ""));
		var hasHandle = false;
		try
		{
			hasHandle = mutex.WaitOne(Timeout.Infinite, false);
			if (File.Exists(fileName))
			{
				return;
			}

			File.WriteAllText(fileName, fileContents);
		}
		catch (Exception)
		{
			throw new InvalidOleVariantTypeException("Error writing file");
		}
		finally
		{
			if (hasHandle)
			{
				mutex.ReleaseMutex();
			}
		}
	}

	public static bool FileExists(string fileName)
	{
		fileName = System.IO.Path.Combine(Path, fileName);
		return File.Exists(fileName);
	}
	public static string GetMimeType(string fileName)
	{
		fileName = System.IO.Path.Combine(Path, fileName);
		if (!File.Exists(fileName))
		{
			return string.Empty;
		}
		var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

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
