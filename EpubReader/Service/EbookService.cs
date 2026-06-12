using System.Text;
using Microsoft.Maui.Graphics.Skia;
using VersOne.Epub;
using VersOne.Epub.Options;
using VersOne.Epub.Schema;
using Point = Microsoft.Maui.Graphics.Point;

namespace EpubReader.Service;

/// <summary>
/// Provides services for handling eBook files, specifically EPUB format, including retrieving book listings and opening
/// eBooks.
/// </summary>
/// <remarks>The <see cref="EbookService"/> class offers static methods to interact with EPUB files, allowing
/// users to extract metadata and content. It supports operations such as retrieving book listings from file paths or
/// streams and opening eBooks asynchronously. The service handles missing content files gracefully by suppressing
/// related exceptions and logs errors encountered during operations.</remarks>
public static partial class EbookService
{
	#region Constants and Static Fields

	static readonly string[] fontExtensions = [".ttf", ".otf", ".woff", ".woff2"];

	static readonly List<string> jsImports = ["Container.js"];

	static readonly string[] requiredFiles =
	[
		"Container.js",
		"ReadiumCSS-default.css",
		"ReadiumCSS-before.css",
		"ReadiumCSS-after.css",
		"ReaderFonts.css",
		"index.html",
		"favicon.ico",
		"EpubText.css",
		"EpubText.js",
		"OpenDyslexic3-Regular.ttf",
		"arial.ttf",
		"times.ttf",
		"comic.ttf",
		"georgia.ttf",
		"cour.ttf",
		"trebuc.ttf",
		"Helvetica.ttf",
		"verdana.ttf",
		"tahoma.ttf"
	];

	static readonly ILogger logger = AppLogger.CreateLogger(nameof(EbookService));

	const int coverImageWidth = 200;
	const int coverImageHeight = 400;

	#endregion

	#region Public Methods

	/// <summary>
	/// Retrieves a book listing from the specified file path.
	/// </summary>
	/// <param name="path">The file path to the ePub book. Must be a valid path to an ePub file.</param>
	/// <returns>A <see cref="Book"/> object containing the title, file path, and cover image of the book. Returns <see
	/// langword="null"/> if the book cannot be opened or an error occurs.</returns>
	public static async Task<Book?> GetListingAsync(string path)
	{
		EpubBookRef? book = await OpenEpubBookAsync(path);
		return book is null ? null : await CreateBookListingAsync(book, path).ConfigureAwait(false);
	}

	/// <summary>
	/// Retrieves a <see cref="Book"/> object from the specified EPUB file stream.
	/// </summary>
	/// <param name="stream">The input stream containing the EPUB file data. Must not be null.</param>
	/// <param name="path">The file path associated with the EPUB file, used for setting the <see cref="Book.FilePath"/> property.</param>
	/// <returns>A <see cref="Book"/> object containing the title, file path, and cover image of the EPUB book. Returns <see
	/// langword="null"/> if the EPUB file cannot be opened or processed.</returns>
	public static async Task<Book?> GetListingAsync(Stream stream, string path)
	{
		EpubBookRef? book = await OpenEpubBook(stream).ConfigureAwait(false);
		return book is null ? null : await CreateBookListingAsync(book, path).ConfigureAwait(false);
	}

	/// <summary>
	/// Asynchronously opens an eBook from the specified file path and returns a <see cref="Book"/> object representing the
	/// eBook's content.
	/// </summary>
	/// <param name="path">The file path to the eBook to be opened. Must be a valid path to an ePub file.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Book"/> object with the
	/// eBook's metadata, content, and resources. Returns <see langword="null"/> if the eBook cannot be opened.</returns>
	public static async Task<Book?> OpenEbookAsync(string path)
	{
		EpubBookRef? book = await OpenEpubBookAsync(path).ConfigureAwait(false);
		return book is null ? null : await CreateFullBookAsync(book, path).ConfigureAwait(false);
	}

	/// <summary>
	/// Combines all chapters of the specified <see cref="Book"/> into a single HTML document string
	/// with all CSS styles embedded inline.
	/// </summary>
	/// <param name="book">
	/// The book to combine. <see cref="Book.Chapters"/> and <see cref="Book.Css"/> must be populated
	/// (i.e. the book must have been opened via <see cref="OpenEbookAsync"/>).
	/// </param>
	/// <returns>
	/// A string containing a complete HTML document where each chapter's body content is wrapped in a
	/// <c>&lt;section&gt;</c> element and all CSS is embedded as <c>&lt;style&gt;</c> blocks.
	/// </returns>
	public static string CombineChapters(Book book)
	{
		ArgumentNullException.ThrowIfNull(book);

		StringBuilder sb = new();
		Dictionary<string, string> chapterTargets = new(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < book.Chapters.Count; i++)
		{
			Chapter? chapter = book.Chapters[i];
			if (string.IsNullOrWhiteSpace(chapter.FileName))
			{
				continue;
			}

			string chapterFileName = Path.GetFileName(chapter.FileName);
			if (string.IsNullOrWhiteSpace(chapterFileName) || chapterTargets.ContainsKey(chapterFileName))
			{
				continue;
			}

			chapterTargets[chapterFileName] = Path.GetFileNameWithoutExtension(chapter.FileName);
		}

		sb.AppendLine("<!DOCTYPE html>");
		sb.AppendLine("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
		sb.AppendLine("<head>");
		sb.AppendLine("<meta charset=\"UTF-8\"/>");
		sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no\"/>");
		sb.AppendLine($"<title>{System.Net.WebUtility.HtmlEncode(book.Title)}</title>");

		// ReadiumCSS must bracket the book's own CSS so properties cascade correctly.
		sb.AppendLine("<link rel=\"stylesheet\" href=\"ReadiumCSS-before.css\"/>");
		sb.AppendLine("<link rel=\"stylesheet\" href=\"ReaderFonts.css\"/>");
		foreach (Css css in book.Css)
		{
			sb.AppendLine($"<link rel=\"stylesheet\" href=\"{css.FileName}\"/>");
		}
		sb.AppendLine("<link rel=\"stylesheet\" href=\"ReadiumCSS-after.css\"/>");

		// Container.js runs inside the iframe and bridges navigation events to the parent frame.
		sb.AppendLine("<script src=\"Container.js\"></script>");

		// All sections are hidden by default; showSection() in EpubText.js reveals the active one.
		sb.AppendLine("<style>section[data-chapter-index]{display:none}</style>");

		sb.AppendLine("</head>");
		sb.AppendLine("<body>");

		for (int i = 0; i < book.Chapters.Count; i++)
		{
			Chapter chapter = book.Chapters[i];
			if (string.IsNullOrEmpty(chapter.HtmlFile))
			{
				continue;
			}

			string chapterId = Path.GetFileNameWithoutExtension(chapter.FileName);
			sb.AppendLine($"<section id=\"{chapterId}\"");
			sb.AppendLine($"         data-chapter-index=\"{i}\"");
			sb.AppendLine($"         data-chapter-title=\"{System.Net.WebUtility.HtmlEncode(chapter.Title)}\"");
			sb.AppendLine($"         data-chapter-filename=\"{chapter.FileName}\">");
			sb.AppendLine(HtmlAgilityPackExtensions.PrepareBodyContentForCombinedDocument(chapter.HtmlFile, chapterId, chapterTargets));
			sb.AppendLine("</section>");
		}

		sb.AppendLine("</body>");
		sb.AppendLine("</html>");

		return sb.ToString();
	}

	#endregion

	#region Private Helper Methods - Book Creation

	static async Task<Book> CreateBookListingAsync(EpubBookRef book, string path)
	{
		List<string> Authors = ExtractAuthors(book);
		byte[] coverImage = await book.ReadCoverAsync().ConfigureAwait(false) ?? GenerateCoverImage(book.Title);
		return new Book
		{
			Author = Authors[0],
			Title = book.Title,
			FilePath = path,
			Description = ProcessDescription(book.Description),
			CoverImage = coverImage,
		};
	}

	static async Task<Book> CreateFullBookAsync(EpubBookRef book, string path)
	{
		List<SharedEpubFiles> sharedFiles = await GetSharedFilesAsync().ConfigureAwait(false);
		List<EpubFonts> fonts = await ExtractFontsAsync(book).ConfigureAwait(false);
		string description = ProcessDescription(book.Description);
		byte[] coverImage = await book.ReadCoverAsync().ConfigureAwait(false) ?? GenerateCoverImage(book.Title);
		List<Css> cssFiles = await ExtractCssFiles(book).ConfigureAwait(false);
		List<Chapter> chapters = await GetChaptersAsync(book).ConfigureAwait(false);
		List<Models.Image> images = await ExtractImages(book);
		List<string> authors = ExtractAuthors(book);
		MediaOverlayParseResult mediaOverlayResult = await MediaOverlayParser.ParseAsync(book).ConfigureAwait(false);
		List<MediaOverlayAudioResource> mediaOverlayAudio = await ExtractMediaOverlayAudioAsync(book, mediaOverlayResult.Documents).ConfigureAwait(false);
		List<EpubFonts> additionalFonts = sharedFiles.SelectMany(f => f.FileName.EndsWith(".ttf", StringComparison.InvariantCultureIgnoreCase) ||
														  f.FileName.EndsWith(".otf", StringComparison.InvariantCultureIgnoreCase) ?
														  new[] { new EpubFonts
														  {
															  Content = f.Content ?? [],
															  FileName = f.FileName,
															  FontFamily = Path.GetFileNameWithoutExtension(f.FileName)
														  } } : []).ToList();
		fonts.AddRange(additionalFonts);

		Book resultBook = new()
		{
			Title = book.Title.Trim(),
			Author = authors[0],
			FilePath = path,
			Files = sharedFiles,
			Fonts = fonts,
			Description = description,
			CoverImage = coverImage,
			Chapters = chapters,
			Images = images,
			Css = cssFiles,
			MediaOverlays = [.. mediaOverlayResult.Documents],
			MediaOverlayAudio = mediaOverlayAudio,
			MediaOverlayActiveClass = mediaOverlayResult.ActiveClass,
			MediaOverlayPlaybackActiveClass = mediaOverlayResult.PlaybackActiveClass,
			MediaOverlayNarrator = mediaOverlayResult.Narrator,
			MediaOverlayDuration = mediaOverlayResult.Duration,
		};

		resultBook.CombinedHtml = CombineChapters(resultBook);
		return resultBook;
	}

	#endregion

	#region Private Helper Methods - EPUB Operations

	static async Task<EpubBookRef?> OpenEpubBookAsync(string path)
	{
		try
		{
			return await VersOne.Epub.EpubReader.OpenBookAsync(path, EpubReaderOptionsPreset.IGNORE_ALL_ERRORS).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.Error($"Get Listing Error: {ex.Message}");
			return null;
		}
	}

	static async Task<EpubBookRef?> OpenEpubBook(Stream stream)
	{
		try
		{
			return await VersOne.Epub.EpubReader.OpenBookAsync(stream, EpubReaderOptionsPreset.IGNORE_ALL_ERRORS).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.Error($"Get Listing Error: {ex.Message}");
			return null;
		}
	}

	#endregion

	#region Private Helper Methods - Content Extraction

	static async Task<List<SharedEpubFiles>> GetSharedFilesAsync()
	{
		List<SharedEpubFiles> sharedFiles = new();

		foreach (string fileName in requiredFiles)
		{
			SharedEpubFiles? sharedFile = await GetSharedFileAsync(fileName).ConfigureAwait(false);
			if (sharedFile is not null)
			{
				sharedFiles.Add(sharedFile);
			}
		}

		return sharedFiles;
	}

	static async Task<SharedEpubFiles?> GetSharedFileAsync(string fileName, CancellationToken cancellation = default)
	{
		bool exists = await FileSystem.AppPackageFileExistsAsync(fileName).ConfigureAwait(false);
		if (!exists)
		{
			return null;
		}

		await using Stream stream = await FileSystem.OpenAppPackageFileAsync(fileName).ConfigureAwait(false);
		using StreamReader reader = new(stream);
		using MemoryStream memoryStream = new();
		await reader.BaseStream.CopyToAsync(memoryStream, cancellation).ConfigureAwait(false);
		byte[] bytes = memoryStream.ToArray();

		SharedEpubFiles sharedFile = new() { FileName = fileName };

		if (StreamExtensions.IsText(fileName))
		{
			sharedFile.HTMLContent = Encoding.UTF8.GetString(bytes);
		}
		else if (StreamExtensions.IsBinary(fileName))
		{
			sharedFile.Content = bytes;
		}
		else
		{
			return null;
		}

		return sharedFile;
	}

	static async Task<List<EpubFonts>> ExtractFontsAsync(EpubBookRef book)
	{
		List<EpubFonts> fonts = new();
		IEnumerable<EpubLocalContentFileRef> fontFiles = book.Content.AllFiles.Local.Where(IsFontFile);

		foreach (EpubLocalContentFileRef? fontFile in fontFiles)
		{
			EpubFonts font = new()
			{
				Content = await fontFile.ReadContentAsBytesAsync().ConfigureAwait(false),
				FileName = Path.GetFileName(fontFile.FilePath),
				FontFamily = Path.GetFileNameWithoutExtension(fontFile.FilePath)
			};
			fonts.Add(font);
		}

		return fonts;
	}

	static bool IsFontFile(EpubLocalContentFileRef file)
	{
		return fontExtensions.Any(ext =>
			file.FilePath.Contains(ext, StringComparison.InvariantCultureIgnoreCase));
	}

	static string ProcessDescription(string? description)
	{
		if (string.IsNullOrEmpty(description))
		{
			return string.Empty;
		}

		return description.Contains("<html>", StringComparison.InvariantCultureIgnoreCase)
			? description
			: $"<html><body>{description}</body></html>";
	}

	static async Task<List<Css>> ExtractCssFiles(EpubBookRef book)
	{
		List<Css> result = new();
		foreach (EpubLocalContentFileRef? file in book.Content.AllFiles.Local.Where(file => file.FilePath.EndsWith(".css", StringComparison.InvariantCultureIgnoreCase)))
		{
			Css css = new()
			{
				FileName = Path.GetFileName(file.FilePath),
				Content = ProcessCssFiles(await file.ReadContentAsTextAsync().ConfigureAwait(false)),
			};
			result.Add(css);
		}

		return result;
	}

	static async Task<List<Models.Image>> ExtractImages(EpubBookRef book)
	{
		List<Models.Image> images = new();

		foreach (EpubLocalByteContentFileRef image in book.Content.Images.Local)
		{
			Models.Image img = new()
			{
				FileName = Path.GetFileName(image.FilePath),
				Content = await image.ReadContentAsBytesAsync().ConfigureAwait(false)
			};
			images.Add(img);
		}

		return images;
	}

	static async Task<List<MediaOverlayAudioResource>> ExtractMediaOverlayAudioAsync(EpubBookRef book, IReadOnlyList<MediaOverlayDocument> documents)
	{
		List<MediaOverlayAudioResource> resources = new();
		if (documents.Count == 0)
		{
			logger.Info("No media overlay documents available for audio extraction.");
			return resources;
		}

		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
		logger.Info($"Extracting media overlay audio from {documents.Count} document(s).");

		foreach (MediaOverlayDocument document in documents)
		{
			int extractedForDocument = 0;
			int missingForDocument = 0;
			foreach (string? source in document.FlattenedNodes
												.Select(node => node.Audio?.Source)
												.Where(src => !string.IsNullOrWhiteSpace(src)))
			{
				if (!TryResolveMediaOverlayAudioFile(book, document, source!, out EpubLocalContentFileRef? file, out string? normalized))
				{
					logger.Warn($"Missing media overlay audio resource: {source}");
					missingForDocument++;
					continue;
				}

				if (!seen.Add(normalized))
				{
					continue;
				}
				try
				{
					byte[] bytes = await file.ReadContentAsBytesAsync().ConfigureAwait(false);
					resources.Add(new MediaOverlayAudioResource
					{
						RelativePath = source!,
						NormalizedPath = normalized,
						Content = bytes,
						ContentType = StreamExtensions.GetMimeType(source!)
					});
					extractedForDocument++;
				}
				catch (Exception ex)
				{
					logger.Warn($"Failed reading media overlay audio '{source}': {ex.Message}");
				}
			}

			logger.Info($"Media overlay audio extraction for document '{document.Id}' ({document.Href}): extracted={extractedForDocument}, missing={missingForDocument}, associatedDocs={document.AssociatedContentDocuments.Count}, flattenedNodes={document.FlattenedNodes.Count}");
		}

		logger.Info($"Extracted {resources.Count} unique media overlay audio resource(s).");

		return resources;
	}

	static List<string> ExtractAuthors(EpubBookRef book)
	{
		return [.. book.Schema.Package.Metadata.Creators
			.Where(author => !string.IsNullOrEmpty(author.Creator))
			.Select(author =>   author.Creator )];
	}

	static EpubLocalContentFileRef? FindLocalContentFile(EpubBookRef book, string normalizedPath)
	{
		return book.Content.AllFiles.Local
			.FirstOrDefault(file => MediaOverlayPathHelper.PathsReferToSameFile(file.FilePath, normalizedPath));
	}

	static bool TryResolveMediaOverlayAudioFile(EpubBookRef book, MediaOverlayDocument document, string source, out EpubLocalContentFileRef file, out string normalizedPath)
	{
		ArgumentNullException.ThrowIfNull(book);
		ArgumentNullException.ThrowIfNull(document);

		foreach (string candidate in BuildMediaOverlayPathCandidates(document, source))
		{
			EpubLocalContentFileRef? match = FindLocalContentFile(book, candidate);
			if (match is null)
			{
				continue;
			}

			file = match;
			normalizedPath = MediaOverlayPathHelper.Normalize(match.FilePath);
			return true;
		}

		file = null!;
		normalizedPath = string.Empty;
		return false;
	}

	static IEnumerable<string> BuildMediaOverlayPathCandidates(MediaOverlayDocument document, string source)
	{
		string normalizedSource = CollapseRelativeSegments(MediaOverlayPathHelper.Normalize(source));
		if (!string.IsNullOrEmpty(normalizedSource))
		{
			yield return normalizedSource;
		}

		string normalizedDocumentHref = MediaOverlayPathHelper.Normalize(document.Href);
		string directory = ExtractDirectory(normalizedDocumentHref);
		if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(normalizedSource))
		{
			yield break;
		}

		string combined = CollapseRelativeSegments($"{directory}{normalizedSource}");
		if (!string.Equals(combined, normalizedSource, StringComparison.OrdinalIgnoreCase))
		{
			yield return combined;
		}
	}

	static string ExtractDirectory(string normalizedPath)
	{
		int lastSlash = normalizedPath.LastIndexOf('/') + 1;
		return lastSlash <= 0 ? string.Empty : normalizedPath[..lastSlash];
	}

	static string CollapseRelativeSegments(string normalizedPath)
	{
		if (string.IsNullOrEmpty(normalizedPath))
		{
			return string.Empty;
		}

		string[] segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		Stack<string> stack = new();

		foreach (string segment in segments)
		{
			if (segment == ".")
			{
				continue;
			}

			if (segment == "..")
			{
				if (stack.Count > 0)
				{
					stack.Pop();
				}
				continue;
			}

			stack.Push(segment);
		}

		return string.Join('/', stack.Reverse());
	}

	#endregion

	#region Private Helper Methods - Chapter Processing

	static async Task<List<Chapter>> GetChaptersAsync(EpubBookRef book)
	{
		List<EpubLocalTextContentFileRef> readingOrder = await book.GetReadingOrderAsync().ConfigureAwait(false);
		List<EpubLocalTextContentFileRef> chaptersRef = readingOrder.ToList();
		return await GetChapters(chaptersRef, book);
	}

	static async Task<List<Chapter>> GetChapters(List<EpubLocalTextContentFileRef> chaptersRef, EpubBookRef book)
	{
		List<Chapter> chapters = new();

		// Handle books with split chapters (e.g., Calibre-generated books)
		if (HasSplitChapters(chaptersRef))
		{
			await ProcessSplitChapters(chaptersRef, book, chapters);
			return chapters;
		}

		// Handle books with few non-split chapters
		if (HasFewNonSplitChapters(chaptersRef))
		{
			await ProcessAllChapters(chaptersRef, book, chapters);
			return chapters;
		}

		// Handle standard books with multiple non-split chapters
		await ProcessNonSplitChapters(chaptersRef, book, chapters);
		return chapters;
	}

	static bool HasSplitChapters(List<EpubLocalTextContentFileRef> chaptersRef)
	{
		return chaptersRef.Count(x => x.FilePath.Contains("_split_001")) > 2;
	}

	static bool HasFewNonSplitChapters(List<EpubLocalTextContentFileRef> chaptersRef)
	{
		return chaptersRef.Count(item => !item.FilePath.Contains("_split_")) < 3;
	}

	static async Task ProcessSplitChapters(List<EpubLocalTextContentFileRef> chaptersRef, EpubBookRef book, List<Chapter> chapters)
	{
		IEnumerable<EpubLocalTextContentFileRef> split000Chapters = chaptersRef
			.Where(x => x is not null && x.FilePath.Contains("_split_000"));

		foreach (EpubLocalTextContentFileRef? item in split000Chapters)
		{
			EpubLocalTextContentFileRef? replacementChapter = FindReplacementChapter(chaptersRef, item);
			string htmlContent = replacementChapter is not null ? await replacementChapter.ReadContentAsync().ConfigureAwait(false) : string.Empty;

			if (string.IsNullOrEmpty(htmlContent))
			{
				continue;
			}

			Chapter chapter = CreateChapter(book, htmlContent, item);
			if (string.IsNullOrEmpty(chapter.Title))
			{
				chapter.Title = GetTitle(book, replacementChapter) ?? string.Empty;
			}
			chapters.Add(chapter);
		}
	}


	static async Task ProcessAllChapters(List<EpubLocalTextContentFileRef> chaptersRef, EpubBookRef book, List<Chapter> chapters)
	{
		foreach (EpubLocalTextContentFileRef item in chaptersRef)
		{
			string htmlContent = await item.ReadContentAsync().ConfigureAwait(false);
			chapters.Add(CreateChapter(book, htmlContent, item));
		}
	}

	static async Task ProcessNonSplitChapters(List<EpubLocalTextContentFileRef> chaptersRef, EpubBookRef book, List<Chapter> chapters)
	{
		IEnumerable<EpubLocalTextContentFileRef> nonSplitChapters = chaptersRef.Where(item => !item.FilePath.Contains("_split_"));
		if (nonSplitChapters.Count() != chaptersRef.Count)
		{
			nonSplitChapters = chaptersRef;
		}
		foreach (EpubLocalTextContentFileRef? item in nonSplitChapters)
		{
			string htmlContent = await item.ReadContentAsync().ConfigureAwait(false);
			chapters.Add(CreateChapter(book, htmlContent, item));
		}
	}

	static Chapter CreateChapter(EpubBookRef book, string htmlContent, EpubLocalTextContentFileRef item)
	{
		string processedHtml = ProcessHtml(htmlContent);
		string fileName = Path.GetFileName(item.FilePath);
		string title = GetTitle(book, item) ?? fileName;

		return new Chapter
		{
			HtmlFile = processedHtml,
			FileName = fileName,
			Title = title,
		};
	}

	static EpubLocalTextContentFileRef? FindReplacementChapter(List<EpubLocalTextContentFileRef> chaptersRef, EpubLocalContentFileRef item)
	{
		string replacementPath = item.FilePath.Replace("_split_000", "_split_001");
		return chaptersRef.Find(x => x.FilePath == replacementPath);
	}

	#endregion

	#region Private Helper Methods - Title Extraction

	static string? GetTitle(EpubBookRef book, EpubLocalTextContentFileRef? item)
	{
		if (item is null)
		{
			return null;
		}

		string fileName = Path.GetFileName(item.FilePath);
		if (string.IsNullOrEmpty(fileName))
		{
			return null;
		}

		string? title = book.Schema.Package.EpubVersion switch
		{
			EpubVersion.EPUB_2 => GetEpub2Title(book, fileName),
			EpubVersion.EPUB_3 => GetEpub3Title(book, fileName),
			EpubVersion.EPUB_3_1 => GetEpub31Title(book, fileName),
			_ => GetFallbackTitle(book, item),
		};

		return title ?? GetLegacyEpub2Title(book, fileName);
	}

	static string? GetEpub2Title(EpubBookRef book, string fileName)
	{
		List<Epub2NcxNavigationPoint> epub2Nav = book.Schema.Epub2Ncx?.NavMap?.Items ?? [];

		return epub2Nav.Find(x => x.Content.Source == fileName)?.NavigationLabels[0]?.Text ??
			   epub2Nav.Find(x => Path.GetFileName(x.Content.Source?.Split('#')[0]) == fileName)?.NavigationLabels[0]?.Text;
	}

	static string? GetEpub3Title(EpubBookRef book, string fileName)
	{
		List<Epub3NavLi> epub3Nav = book.Schema.Epub3NavDocument?.Navs[0]?.Ol?.Lis?.ToList() ?? [];

		return epub3Nav.Find(x => x.Anchor?.Href == fileName)?.Anchor?.Text ??
			   epub3Nav.Find(x => Path.GetFileName(x.Anchor?.Href)?.Split('#')[0] == fileName)?.Anchor?.Text;
	}

	static string? GetEpub31Title(EpubBookRef book, string fileName)
	{
		List<Epub3NavLi> epub3Nav = book.Schema.Epub3NavDocument?.Navs[0]?.Ol?.Lis?.ToList() ?? [];
		return epub3Nav.Find(x => x.Anchor?.Href == fileName)?.Anchor?.Text;
	}

	static string? GetFallbackTitle(EpubBookRef book, EpubLocalTextContentFileRef item)
	{
		return book.GetNavigation()?.Find(x => x.Link?.ContentFilePath == item.FilePath)?.Title;
	}

	static string? GetLegacyEpub2Title(EpubBookRef book, string fileName)
	{
		return book.Schema.Epub2Ncx?.NavMap?.Items?
			.Find(x => Path.GetFileName(x.Content.Source) == fileName)?.NavigationLabels[0].Text;
	}

	#endregion

	#region Private Helper Methods - Content Processing

	static string ProcessCssFiles(string cssFile)
	{
		if (string.IsNullOrEmpty(cssFile))
		{
			return string.Empty;
		}

		cssFile = HtmlAgilityPackExtensions.RemoveCalibreReferences(cssFile);
		cssFile = FilePathExtensions.SetFontFilenames(cssFile);
		cssFile = HtmlAgilityPackExtensions.UpdateImagePathsForCSSFiles(cssFile);
		return cssFile;
	}

	static string ProcessHtml(string htmlFile)
	{
		List<string> cssFiles = HtmlAgilityPackExtensions.GetCssFiles(htmlFile);

		htmlFile = HtmlAgilityPackExtensions.RemoveCssLinks(htmlFile);
		htmlFile = HtmlAgilityPackExtensions.AddCssLink(htmlFile, "ReadiumCSS-before.css");
		htmlFile = HtmlAgilityPackExtensions.AddCssLink(htmlFile, "ReaderFonts.css");
		htmlFile = HtmlAgilityPackExtensions.AddCssLinks(htmlFile, cssFiles);

		if (cssFiles.Count == 0)
		{
			htmlFile = HtmlAgilityPackExtensions.AddCssLink(htmlFile, "ReadiumCSS-default.css");
		}

		htmlFile = HtmlAgilityPackExtensions.AddCssLink(htmlFile, "ReadiumCSS-after.css");
		htmlFile = HtmlAgilityPackExtensions.AddJsLinks(htmlFile, jsImports);
		htmlFile = HtmlAgilityPackExtensions.UpdateImageUrl(htmlFile);
		htmlFile = FilePathExtensions.UpdateImagePathsToFilenames(htmlFile);
		htmlFile = FilePathExtensions.UpdateSvgLinks(htmlFile);
		htmlFile = HtmlAgilityPackExtensions.EnsureXmlLang(htmlFile);
		htmlFile = HtmlAgilityPackExtensions.EnsureXhtml1TransitionalDoctype(htmlFile);
		htmlFile = HtmlAgilityPackExtensions.RemoveKoboScriptLinks(htmlFile);
		htmlFile = HtmlAgilityPackExtensions.SetViewportMeta(htmlFile);
		return htmlFile;
	}

	#endregion

	#region Private Helper Methods - Image Processing

	public static byte[] GenerateCoverImage(string title)
	{
		using SkiaBitmapExportContext bmp = new(coverImageWidth, coverImageHeight, 1.0f);
		ICanvas canvas = bmp.Canvas;

		DrawCoverBackground(canvas, bmp.Width, bmp.Height);
		DrawCoverTitle(canvas, title, bmp.Width, bmp.Height);

		return bmp.Image.AsBytes(ImageFormat.Jpeg);
	}

	static void DrawCoverBackground(ICanvas canvas, int width, int height)
	{
		Rect backgroundRectangle = new(0, 0, width, height);
		canvas.FillColor = Colors.White;
		canvas.FillRectangle(backgroundRectangle);
		canvas.StrokeColor = Colors.Black;
		canvas.StrokeSize = 28;
		canvas.DrawRectangle(backgroundRectangle);
	}

	static void DrawCoverTitle(ICanvas canvas, string title, int width, int height)
	{
		Microsoft.Maui.Graphics.Font font = new("Arial");
		const float fontSize = 20;
		canvas.FontSize = fontSize;
		SizeF textSize = canvas.GetStringSize(title, font, fontSize);

		Point point = new(
			x: (width - textSize.Width) / 2,
			y: (height - textSize.Height) / 2);
		Rect textRectangle = new(point, textSize);

		// Draw background rectangle for text
		canvas.FillColor = Colors.Black.WithAlpha(.5f);
		canvas.FillRectangle(textRectangle);
		canvas.StrokeSize = 2;
		canvas.StrokeColor = Colors.Yellow;
		canvas.DrawRectangle(textRectangle);

		// Draw the title text
		canvas.FontSize = fontSize * .7f;
		canvas.FontColor = Colors.White;
		canvas.DrawString(title, textRectangle,
			HorizontalAlignment.Center, VerticalAlignment.Center, TextFlow.ClipBounds);
	}
	#endregion
}