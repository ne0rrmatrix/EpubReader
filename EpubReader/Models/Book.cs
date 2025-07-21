using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace EpubReader.Models;

/// <summary>
/// Represents a book entity with properties for storing metadata and content details.
/// </summary>
/// <remarks>This class is designed to map to a database table named "Book" and includes properties for storing
/// the book's title, file path, current chapter, and cover image path. It also includes collections for related
/// entities such as authors, chapters, and images, which are not persisted in the database.</remarks>
[Table("Book")]
public partial class Book : ObservableObject
{
	/// <summary>
	/// Gets or sets the unique identifier for the entity.
	/// </summary>
	[PrimaryKey, AutoIncrement]
	[Column("Id")]
	public Guid Id { get; set; }

	/// <summary>
	/// Gets or sets the title of the book.
	/// </summary>
	[Column("Title")]
	public string Title { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the file path associated with the entity.
	/// </summary>
	[Column("FilePath")]
	public string FilePath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current chapter number in the sequence.
	/// </summary>
	[Column("CurrentChapter")]
	public int CurrentChapter { get; set; } = 0;

	/// <summary>
	/// Gets or sets the current page number in a paginated list.
	/// </summary>
	[Column("CurrentPage")]
	public int CurrentPage { get; set; } = 0;

	/// <summary>
	/// Gets or sets the file path to the cover image.
	/// </summary>
	[Column("CoverImagePath")]
	public string CoverImagePath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the list of shared EPUB files.
	/// </summary>
	[Ignore]
	public List<SharedEpubFiles> Files { get; set; } = [];

	/// <summary>
	/// Gets or sets the collection of fonts used in the EPUB document.
	/// </summary>
	[Ignore]
	public List<EpubFonts> Fonts { get; set; } = [];

	/// <summary>
	/// Gets or sets the collection of chapters associated with the current entity.
	/// </summary>

	[Ignore]
	public List<Chapter> Chapters { get; set; } = [];

	/// <summary>
	/// Gets or sets the collection of CSS styles associated with the element.
	/// </summary>
	[Ignore]
	public List<Css> Css { get; set; } = [];

	/// <summary>
	/// Gets or sets the cover image as a byte array.
	/// </summary>

	[Ignore]
	public Byte[] CoverImage { get; set; } = [];

	/// <summary>
	/// Gets or sets the author.
	/// </summary>
	[Column("Author")]
	public string Author { get; set; } = string.Empty;


	/// <summary>
	/// Gets or sets the collection of images associated with the entity.
	/// </summary>
	[Ignore]
	public List<Image> Images { get; set; } = [];

	/// <summary>
	/// Gets or sets the description text.
	/// </summary>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the date as a string.
	/// </summary>
	public string Date { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the thumbnail image as a byte array.
	/// </summary>
	[Ignore]
	public string Thumbnail { get; set; } = string.Empty;
	/// <summary>
	/// Gets or sets the URL used for downloading resources.
	/// </summary>
	public string DownloadUrl { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the item is currently in the library.
	/// </summary>
	[Column("IsInLibrary")]
	[ObservableProperty]
	public partial bool IsInLibrary { get; set; } = false;

	/// <summary>
	/// Gets or sets the published date of the book.
	/// </summary>
	[Ignore]
	public DateTime? PublishedDate { get; set; }

	/// <summary>
	/// Gets or sets the book's ISBN if available.
	/// </summary>
	public string Isbn { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the book's language code.
	/// </summary>
	public string Language { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the book's series information.
	/// </summary>
	public string Series { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the book's categories/tags.
	/// </summary>
	[Ignore]
	public List<string> Categories { get; set; } = [];
}