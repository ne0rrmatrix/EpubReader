namespace EpubReader.Models;

public partial class Book
{
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<Author> Authors { get; set; } = [];
    public Byte[] CoverImage { get; set; } = [];
    public List<CSS> Css { get; set; } = [];
    public List<Chapter> Chapters { get; set; } = [];
	public int CurrentPage { get; set; } = 0;
}
