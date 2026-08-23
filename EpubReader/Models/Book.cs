namespace EpubReader.Models;

/// <summary>
/// Represents a book entity with properties for storing metadata and content details.
/// </summary>
/// <remarks>This class is designed to map to a database table named "Book" and includes properties for storing
/// the book's title, file path, current chapter, and cover image path. It also includes collections for related
/// entities such as authors, chapters, and images, which are not persisted in the database.</remarks>
public partial class Book : ObservableObject
{
	/// <summary>
	/// Gets or sets the unique identifier for the entity.
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Gets or sets the title of the book.
	/// </summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the file path associated with the entity.
	/// </summary>
	public string FilePath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current chapter number in the sequence.
	/// </summary>
	public int CurrentChapter { get; set; } = 0;

	/// <summary>
	/// Gets or sets the current page number in a paginated list.
	/// </summary>
	public int CurrentPage { get; set; } = 0;

	// --- Media Overlay playback (local persistence; optional) ---
	public bool? MediaOverlayEnabled { get; set; }

	public int? MediaOverlayChapter { get; set; }

	public int? MediaOverlaySegmentIndex { get; set; }

	public double? MediaOverlayPositionSeconds { get; set; }

	public string? MediaOverlayFragmentId { get; set; }

	/// <summary>
	/// Gets or sets the file path to the cover image.
	/// </summary>
	public string CoverImagePath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the deterministic identifier used for duplicate detection and cross-device sync.
	/// Computed from the SHA-1 hash of the EPUB file's contents by <see cref="Util.BookIdentityService"/>,
	/// so it stays stable even when the file name differs across devices/platforms.
	/// </summary>
	public string SyncId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the list of shared EPUB files.
	/// </summary>
	public List<SharedEpubFiles> Files { get; set; } = [];

	/// <summary>
	/// Gets or sets the collection of fonts used in the EPUB document.
	/// </summary>
	public List<EpubFonts> Fonts { get; set; } = [];

	/// <summary>
	/// Gets or sets the collection of chapters associated with the current entity.
	/// </summary>

	public List<Chapter> Chapters { get; set; } = [];

	/// <summary>
	/// Gets or sets the collection of CSS styles associated with the element.
	/// </summary>
	public List<Css> Css { get; set; } = [];

	/// <summary>
	/// Gets or sets the combined single-page HTML document produced by <see cref="EpubReader.Service.EbookService.CombineChapters"/>.
	/// All chapters are concatenated into one document with each chapter wrapped in a
	/// <c>&lt;section data-chapter-index="N"&gt;</c> element. Only the active section is shown at a time.
	/// </summary>
	public string CombinedHtml { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the cover image as a byte array.
	/// </summary>

	public Byte[] CoverImage { get; set; } = [];

	/// <summary>
	/// Gets or sets the author.
	/// </summary>
	public string Author { get; set; } = string.Empty;


	/// <summary>
	/// Gets or sets the collection of images associated with the entity.
	/// </summary>
	public List<Image> Images { get; set; } = [];

	public List<MediaOverlayDocument> MediaOverlays { get; set; } = [];

	public List<MediaOverlayAudioResource> MediaOverlayAudio { get; set; } = [];

	public string? MediaOverlayActiveClass { get; set; }

	public string? MediaOverlayPlaybackActiveClass { get; set; }

	public string? MediaOverlayNarrator { get; set; }

	public TimeSpan? MediaOverlayDuration { get; set; }

	public bool HasMediaOverlays => MediaOverlays.Count > 0;

	public bool HasNarratedMedia => HasMediaOverlays && MediaOverlayAudio.Count > 0;

	public MediaOverlayAudioResource? FindMediaOverlayAudio(string? path)
	{
		if (!HasNarratedMedia || string.IsNullOrWhiteSpace(path))
		{
			return null;
		}

		string normalized = MediaOverlayPathHelper.Normalize(path);
		string fileName = MediaOverlayPathHelper.ExtractFileName(path);

		return MediaOverlayAudio.FirstOrDefault(resource =>
			string.Equals(resource.NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(MediaOverlayPathHelper.ExtractFileName(resource.NormalizedPath), fileName, StringComparison.OrdinalIgnoreCase));
	}

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
	public string Thumbnail { get; set; } = string.Empty;
	/// <summary>
	/// Gets or sets the URL used for downloading resources.
	/// </summary>
	public string DownloadUrl { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the item is currently in the library.
	/// </summary>
	[ObservableProperty]
	public partial bool IsInLibrary { get; set; } = false;

	/// <summary>
	/// Gets or sets the published date of the book.
	/// </summary>
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
	public List<string> Categories { get; set; } = [];

	/// <summary>
	/// Gets or sets the date when the book was added to the library.
	/// </summary>
	public DateTime DateAdded { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Gets or sets the date when the book was last opened for reading.
	/// </summary>
	public DateTime? LastOpenedDate { get; set; }
}