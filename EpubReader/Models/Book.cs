namespace EpubReader.Models;

public partial class Book
{
    public int CurrentPage { get; set; } = 0;
    public string Title { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
    public List<Author> Authors { get; set; } = [];
    public string CoverImageFileName { get; set; } = string.Empty;
    public Byte[] CoverImage { get; set; } = [];
    public List<CSS> Css { get; set; } = [];
    public List<Chapter> Chapters { get; set; } = [];
}
