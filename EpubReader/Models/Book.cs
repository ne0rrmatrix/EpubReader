using SQLite;
using SQLiteNetExtensions.Attributes;

namespace EpubReader.Models;

[Table("Book")]
public class Book
{
	[PrimaryKey, AutoIncrement, Column("Id")]
	public int Id { get; set; }
	public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
	public int CurrentChapter { get; set; } = 0;

	[Ignore]
	public List<Chapter> Chapters { get; set; } = [];

	[Ignore]
	public List<Css> Css { get; set; } = [];

	[Ignore]
	public Byte[] CoverImage { get; set; } = [];

	[Ignore]
	public List<Author> Authors { get; set; } = [];

	[Ignore]
	public List<Image> Images { get; set; } = [];
}
