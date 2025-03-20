namespace EpubReader.Models;
public class EpubFonts
{
	public string FontFamily { get; set; } = string.Empty;
	public byte[] Content { get; set; } = [];
	public string FileName { get; set; } = string.Empty;
}
