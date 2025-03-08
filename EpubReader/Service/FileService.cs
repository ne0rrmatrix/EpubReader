using MetroLog;

namespace EpubReader.Service;

public static partial class FileService
{
    static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FileService));
    public static readonly string SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpubReader");
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

    public static async Task<string> SaveFile(FileResult result)
    {
		string fileName = string.Empty;
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
			fileName = GetFileName(result.FileName);
			using FileStream output = File.Create(fileName);
			await fileStream.CopyToAsync(output);
			fileStream.Seek(0, SeekOrigin.Begin);
			FileStream.Synchronized(output);
			logger.Info($"File saved: {fileName}");
		}
		catch (Exception ex)
		{
			logger.Error($"Error saving file: {ex.Message}");
			return string.Empty;
		}
        return fileName;
    }
}
