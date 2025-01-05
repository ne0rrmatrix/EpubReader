using MetroLog;

namespace EpubReader.Service;

public static class FileService
{
    static readonly ILogger logger = LoggerFactory.GetLogger(nameof(FileService));
    public static readonly string saveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpubReader");
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
        return Path.Combine(saveDirectory, filename);
    }

    public static async Task<string> SaveFile(FileResult result)
    {
        Directory.CreateDirectory(saveDirectory);
        using Stream fileStream = await result.OpenReadAsync();
        using StreamReader reader = new(fileStream);
        string fileName = GetFileName(result.FileName);
        using FileStream output = File.Create(fileName);
        await fileStream.CopyToAsync(output);
        fileStream.Seek(0, SeekOrigin.Begin);
        FileStream.Synchronized(output);
        logger.Info($"File saved: {fileName}");
        return fileName;
    }
}
