namespace EpubReader.Models;

public class Chapter
{
    public string Title { get; set; } = string.Empty;
    public string HtmlFile { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
	public List<Page> Pages { get; set; } = [];
}