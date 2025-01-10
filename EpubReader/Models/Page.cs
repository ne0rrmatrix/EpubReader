namespace EpubReader.Models;

public class Page
{
	public int Id { get; set; }
	public string NavPoint { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
}
