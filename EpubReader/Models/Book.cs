using SQLite;

namespace EpubReader.Models;

[Table("Book")]
public class Book
{
	[PrimaryKey, AutoIncrement]
	[Column("Id")]
	public Guid Id { get; set; }
	[Column("Title")]
	public string Title { get; set; } = string.Empty;
	[Column("FilePath")]
	public string FilePath { get; set; } = string.Empty;
	[Column("CurrentChapter")]
	public int CurrentChapter { get; set; } = 0;
	[Column("CoverImagePath")]
	public string CoverImagePath { get; set; } = string.Empty;
	[Ignore]
	public List<SharedEpubFiles> Files { get; set; } = [];
	[Ignore]
	public List<EpubFonts> Fonts { get; set; } = [];

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

	public string Description { get; set; } = string.Empty;

}
