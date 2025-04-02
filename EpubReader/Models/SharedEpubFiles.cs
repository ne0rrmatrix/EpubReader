namespace EpubReader.Models;
public class SharedEpubFiles
{
	public string FileName { get; set; } = string.Empty;
	public string HTMLContent { get; set; } = string.Empty;
	public byte[] Content { get; set; } = [];
}
