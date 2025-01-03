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

    /// <summary>
    /// Get file name for a string <see cref="string"/>
    /// </summary>
    /// <param name="name">A file name <see cref="string"/></param>
    /// <returns>Filename <see cref="string"/> with file extension</returns>
    public static string GetFileName(string name)
    {
        var filename = Path.GetFileName(name);
        return Path.Combine(saveDirectory, filename);
    }

    /// <summary>
    /// Save file to local storage
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
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
    public static async Task<string> SaveFile(Byte[] bytes, string fileName, CancellationToken cancellationToken)
    {
        logger.Info($"Saving file: {fileName}");
        using var stream = new MemoryStream(bytes);
        if (File.Exists(fileName))
        {
            logger.Info($"File already exists: {fileName}");
            return string.Empty;
        }
        await File.WriteAllBytesAsync(fileName, bytes, cancellationToken).ConfigureAwait(false);
        logger.Info($"Saved file: {fileName}");
        return fileName;
    }
    public static async Task<Byte[]> ReadFileBytes(string fileName, CancellationToken cancellationToken)
    {
        if (!File.Exists(fileName))
        {
            logger.Error($"File not found: {fileName}");
            return [];
        }
        var result = await File.ReadAllBytesAsync(fileName, cancellationToken).ConfigureAwait(false);
        logger.Info($"Read file: {fileName}");
        return result;
    }
}
