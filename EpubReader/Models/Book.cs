using Newtonsoft.Json;
using SQLite;
using SQLiteNetExtensions.Attributes;

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
	[Column("Files")]
	public string SerializedFiles
	{
		get => JsonConvert.SerializeObject(Files);
		set => Files = JsonConvert.DeserializeObject<List<SharedEpubFiles>>(value) ?? [];
	}
	[Ignore]
	public List<EpubFonts> Fonts { get; set; } = [];
	[Column("Fonts")]
	public string SerializedFonts
	{
		get => JsonConvert.SerializeObject(Fonts);
		set => Fonts = JsonConvert.DeserializeObject<List<EpubFonts>>(value) ?? [];
	}

	[Ignore]
	public List<Chapter> Chapters { get; set; } = [];
	[Column("Chapters")]
	public string SerializedChapters
	{
		get => JsonConvert.SerializeObject(Chapters);
		set => Chapters = JsonConvert.DeserializeObject<List<Chapter>>(value) ?? [];
	}

	[Ignore]
	public List<Css> Css { get; set; } = [];

	[Column("Css")]
	public string SerializedCss
	{
		get => JsonConvert.SerializeObject(Css);
		set => Css = JsonConvert.DeserializeObject<List<Css>>(value) ?? [];
	}

	[Ignore]
	public Byte[] CoverImage { get; set; } = [];

	[Ignore]
	public List<Author> Authors { get; set; } = [];

	[Ignore]
	public List<Image> Images { get; set; } = [];

	[Column("Images")]
	public string SerializedImages
	{
		get => JsonConvert.SerializeObject(Images);
		set => Images = JsonConvert.DeserializeObject<List<Image>>(value) ?? [];
	}
}
