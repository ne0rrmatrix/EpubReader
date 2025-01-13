using SQLite;
using SQLiteNetExtensions.Attributes;

namespace EpubReader.Models;

[Table("Book")]
public partial class Book
{
	[PrimaryKey, AutoIncrement, Column("Id")]
	public int Id { get; set; }
	public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
	public int CurrentPage { get; set; } = 0;
	public int TotalPages { get; set; } = 0;
	public int CurrentChapter { get; set; } = 0;
	public string CoverUrl { get; set; } = string.Empty;

	[Ignore]
	public List<Chapter> Chapters { get; set; } = [];

	[Ignore]
	public List<CSS> Css { get; set; } = [];

	[Ignore]
	public Byte[] CoverImage { get; set; } = [];

	[Ignore]
	public List<Author> Authors { get; set; } = [];

	[Ignore]
	public List<Image> Images { get; set; } = [];
}
