namespace EpubReader.Util;

public static class TempFileCreator
{
	public static string CreateTempFile(string? content, string fileName,string path)
	{
		var filePath = Path.Combine(path, fileName);
		if(File.Exists(filePath))
		{
			return filePath;
		}
		Directory.CreateDirectory(path);
		File.WriteAllText(filePath, content);
		return filePath;
	}
	public static string CreateTempFile(byte[] content, string fileName, string path)
	{
		var filePath = Path.Combine(path, fileName);
		if(File.Exists(filePath))
		{
			return filePath;
		}
		Directory.CreateDirectory(path);
		File.WriteAllBytes(filePath, content);
		return filePath;
	}
}
