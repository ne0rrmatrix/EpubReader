using System.Text;
using EpubReader.Models;
using EpubReader.Util;
using MetroLog;
using Microsoft.Maui.Graphics.Skia;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
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
		"ReadiumCSS-config.css",
		"index.html",
		"favicon.ico",
		"EpubText.css",
		"EpubText.js",
	];

	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(EbookService));

	static readonly EpubReaderOptions options = CreateEpubReaderOptions();

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
		var book = await OpenEpubBookAsync(path);
		return book is null ? null : await CreateBookListingAsync(book, path);
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
		var book = await OpenEpubBook(stream);
		return book is null ? null : await CreateBookListingAsync(book, path);
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
		var book = await OpenEpubBookAsync(path);
		return book is null ? null : await CreateFullBookAsync(book, path);
	}

	#endregion

	#region Private Helper Methods - Book Creation

	static async Task<Book> CreateBookListingAsync(EpubBookRef book, string path)
	{
		var coverBytes = await book.ReadCoverAsync() ?? GenerateCoverImage(book.Title);
		var coverImage = ResizeImageForCollectionView(coverBytes);

		return new Book
		{
			Title = book.Title,
			FilePath = path,
			CoverImage = coverImage,
		};
	}

	static async Task<Book> CreateFullBookAsync(EpubBookRef book, string path)
	{
		var sharedFiles = await GetSharedFilesAsync();
		var fonts = await ExtractFontsAsync(book);
		var description = ProcessDescription(book.Description);
		var coverImage = await book.ReadCoverAsync().ConfigureAwait(false) ?? GenerateCoverImage(book.Title);
		var cssFiles = ExtractCssFiles(book);
		var chapters = await GetChaptersAsync(book);
		var images = ExtractImages(book);
		var authors = ExtractAuthors(book);

		return new Book
		{
			Title = book.Title.Trim(),
			Authors = authors,
			FilePath = path,
			Files = sharedFiles,
			Fonts = fonts,
			Description = description,
			CoverImage = coverImage,
			Chapters = chapters,
			Images = images,
			Css = cssFiles,
		};
	}

	#endregion

	#region Private Helper Methods - EPUB Operations

	static async Task<EpubBookRef?> OpenEpubBookAsync(string path)
	{
		try
		{
			ConfigureContentFileMissingHandler();
			return await VersOne.Epub.EpubReader.OpenBookAsync(path, options);
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
			ConfigureContentFileMissingHandler();
			return await VersOne.Epub.EpubReader.OpenBookAsync(stream, options);
		}
		catch (Exception ex)
		{
			logger.Error($"Get Listing Error: {ex.Message}");
			return null;
		}
	}


	static void ConfigureContentFileMissingHandler()
	{
		options.ContentReaderOptions.ContentFileMissing += (sender, e) => e.SuppressException = true;
	}

	static EpubReaderOptions CreateEpubReaderOptions()
	{
		return new EpubReaderOptions
		{
			BookCoverReaderOptions = new() { Epub2MetadataIgnoreMissingManifestItem = true },
			Epub2NcxReaderOptions = new() { IgnoreMissingContentForNavigationPoints = true },
			PackageReaderOptions = new() { IgnoreMissingToc = true },
			SpineReaderOptions = new() { IgnoreMissingManifestItems = true },
			XmlReaderOptions = new() { SkipXmlHeaders = true },
		};
	}

	#endregion

	#region Private Helper Methods - Content Extraction

	static async Task<List<SharedEpubFiles>> GetSharedFilesAsync()
	{
		var sharedFiles = new List<SharedEpubFiles>();

		foreach (var fileName in requiredFiles)
		{
			var sharedFile = await GetSharedFileAsync(fileName);
			if (sharedFile is not null)
			{
				sharedFiles.Add(sharedFile);
			}
		}

		return sharedFiles;
	}

	static async Task<SharedEpubFiles?> GetSharedFileAsync(string fileName, CancellationToken cancellation = default)
	{
		var exists = await FileSystem.AppPackageFileExistsAsync(fileName);
		if (!exists)
		{
			return null;
		}

		await using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
		using var reader = new StreamReader(stream);
		using var memoryStream = new MemoryStream();
		await reader.BaseStream.CopyToAsync(memoryStream, cancellation);
		var bytes = memoryStream.ToArray();

		var sharedFile = new SharedEpubFiles { FileName = fileName };

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
		var fonts = new List<EpubFonts>();
		var fontFiles = book.Content.AllFiles.Local.Where(IsFontFile);

		foreach (var fontFile in fontFiles)
		{
			var font = new EpubFonts
			{
				Content = await fontFile.ReadContentAsBytesAsync(),
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

	static List<Css> ExtractCssFiles(EpubBookRef book)
	{
		return [.. book.Content.AllFiles.Local
			.Where(file => file.FilePath.EndsWith(".css", StringComparison.InvariantCultureIgnoreCase))
			.Select(file => new Css
			{
				FileName = Path.GetFileName(file.FilePath),
				Content = ProcessCssFiles(file.ReadContentAsText()),
			})];
	}

	static List<Models.Image> ExtractImages(EpubBookRef book)
	{
		return [.. book.Content.Images.Local
			.Select(image => new Models.Image
			{
				FileName = Path.GetFileName(image.FilePath),
				Content = image.ReadContentAsBytes(),
			})];
	}

	static List<Author> ExtractAuthors(EpubBookRef book)
	{
		return [.. book.AuthorList
			.Where(author => !string.IsNullOrEmpty(author))
			.Select(author => new Author { Name = author })];
	}

	#endregion

	#region Private Helper Methods - Chapter Processing

	static async Task<List<Chapter>> GetChaptersAsync(EpubBookRef book)
	{
		var readingOrder = await book.GetReadingOrderAsync();
		var chaptersRef = readingOrder.ToList();

		return GetChapters(chaptersRef, book);
	}

	static List<Chapter> GetChapters(List<EpubLocalTextContentFileRef> chaptersRef, EpubBookRef book)
	{
		var chapters = new List<Chapter>();

		// Handle books with split chapters (e.g., Calibre-generated books)
		if (HasSplitChapters(chaptersRef))
		{
			ProcessSplitChapters(chaptersRef, book, chapters);
			return chapters;
		}

		// Handle books with few non-split chapters
		if (HasFewNonSplitChapters(chaptersRef))
		{
			ProcessAllChapters(chaptersRef, book, chapters);
			return chapters;
		}

		// Handle standard books with multiple non-split chapters
		ProcessNonSplitChapters(chaptersRef, book, chapters);
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

	static void ProcessSplitChapters(List<EpubLocalTextContentFileRef> chaptersRef, EpubBookRef book, List<Chapter> chapters)
	{
		var split000Chapters = chaptersRef
			.Where(x => x is not null && x.FilePath.Contains("_split_000"));

		foreach (var item in split000Chapters)
		{
			var replacementChapter = FindReplacementChapter(chaptersRef, item);
			var htmlContent = replacementChapter?.ReadContent() ?? string.Empty;

			if (string.IsNullOrEmpty(htmlContent))
			{
				continue;
			}

			var chapter = CreateChapter(book, htmlContent, item);
			if (string.IsNullOrEmpty(chapter.Title))
			{
				chapter.Title = GetTitle(book, replacementChapter) ?? string.Empty;
			}
			chapters.Add(chapter);
		}
	}

	static void ProcessAllChapters(List<EpubLocalTextContentFileRef> chaptersRef, EpubBookRef book, List<Chapter> chapters)
	{
		foreach (var item in chaptersRef)
		{
			var htmlContent = item.ReadContent();
			chapters.Add(CreateChapter(book, htmlContent, item));
		}
	}

	static void ProcessNonSplitChapters(List<EpubLocalTextContentFileRef> chaptersRef, EpubBookRef book, List<Chapter> chapters)
	{
		var nonSplitChapters = chaptersRef.Where(item => !item.FilePath.Contains("_split_"));

		foreach (var item in nonSplitChapters)
		{
			var htmlContent = item.ReadContent();
			chapters.Add(CreateChapter(book, htmlContent, item));
		}
	}

	static Chapter CreateChapter(EpubBookRef book, string htmlContent, EpubLocalTextContentFileRef item)
	{
		var processedHtml = ProcessHtml(htmlContent);
		var fileName = Path.GetFileName(item.FilePath);
		var title = GetTitle(book, item) ?? string.Empty;

		return new Chapter
		{
			HtmlFile = processedHtml,
			FileName = fileName,
			Title = title,
		};
	}

	static EpubLocalTextContentFileRef? FindReplacementChapter(List<EpubLocalTextContentFileRef> chaptersRef, EpubLocalContentFileRef item)
	{
		var replacementPath = item.FilePath.Replace("_split_000", "_split_001");
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

		var fileName = Path.GetFileName(item.FilePath);
		if (string.IsNullOrEmpty(fileName))
		{
			return null;
		}

		var title = book.Schema.Package.EpubVersion switch
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
		var epub2Nav = book.Schema.Epub2Ncx?.NavMap?.Items ?? [];

		return epub2Nav.Find(x => x.Content.Source == fileName)?.NavigationLabels[0]?.Text ??
			   epub2Nav.Find(x => Path.GetFileName(x.Content.Source?.Split('#')[0]) == fileName)?.NavigationLabels[0]?.Text;
	}

	static string? GetEpub3Title(EpubBookRef book, string fileName)
	{
		var epub3Nav = book.Schema.Epub3NavDocument?.Navs[0]?.Ol?.Lis?.ToList() ?? [];

		return epub3Nav.Find(x => x.Anchor?.Href == fileName)?.Anchor?.Text ??
			   epub3Nav.Find(x => Path.GetFileName(x.Anchor?.Href)?.Split('#')[0] == fileName)?.Anchor?.Text;
	}

	static string? GetEpub31Title(EpubBookRef book, string fileName)
	{
		var epub3Nav = book.Schema.Epub3NavDocument?.Navs[0]?.Ol?.Lis?.ToList() ?? [];
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
		var cssFiles = HtmlAgilityPackExtensions.GetCssFiles(htmlFile);
		htmlFile = HtmlAgilityPackExtensions.RemoveCssLinks(htmlFile);
		htmlFile = HtmlAgilityPackExtensions.AddCssLink(htmlFile, "ReadiumCSS-before.css");
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
		return htmlFile;
	}

	#endregion

	#region Private Helper Methods - Image Processing

	static byte[] GenerateCoverImage(string title)
	{
		using var bmp = new SkiaBitmapExportContext(coverImageWidth, coverImageHeight, 1.0f);
		ICanvas canvas = bmp.Canvas;

		DrawCoverBackground(canvas, bmp.Width, bmp.Height);
		DrawCoverTitle(canvas, title, bmp.Width, bmp.Height);

		return bmp.Image.AsBytes(ImageFormat.Jpeg);
	}

	static void DrawCoverBackground(ICanvas canvas, int width, int height)
	{
		var backgroundRectangle = new Rect(0, 0, width, height);
		canvas.FillColor = Colors.White;
		canvas.FillRectangle(backgroundRectangle);
		canvas.StrokeColor = Colors.Black;
		canvas.StrokeSize = 28;
		canvas.DrawRectangle(backgroundRectangle);
	}

	static void DrawCoverTitle(ICanvas canvas, string title, int width, int height)
	{
		var font = new Microsoft.Maui.Graphics.Font("Arial");
		const float fontSize = 20;
		canvas.FontSize = fontSize;
		var textSize = canvas.GetStringSize(title, font, fontSize);

		var point = new Point(
			x: (width - textSize.Width) / 2,
			y: (height - textSize.Height) / 2);
		var textRectangle = new Rect(point, textSize);

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

	static byte[] ResizeImageForCollectionView(byte[] imageBytes)
	{
		try
		{
			using var inputStream = new MemoryStream(imageBytes);
			using var image = SixLabors.ImageSharp.Image.Load(inputStream);

			var scale = CalculateImageScale(image.Width, image.Height);
			var newWidth = (int)(image.Width * scale);
			var newHeight = (int)(image.Height * scale);

			using var resizedImage = CreateResizedImage(image, newWidth, newHeight);
			return SaveImageAsPng(resizedImage);
		}
		catch (Exception ex)
		{
			logger.Error($"Error resizing image for collection view: {ex.Message}");
			return imageBytes;
		}
	}

	static float CalculateImageScale(int originalWidth, int originalHeight)
	{
		bool needsUpscaling = originalWidth < coverImageWidth || originalHeight < coverImageHeight;
		var scaleX = (float)coverImageWidth / originalWidth;
		var scaleY = (float)coverImageHeight / originalHeight;

		return needsUpscaling ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
	}

	static Image<SixLabors.ImageSharp.PixelFormats.Rgba32> CreateResizedImage(
		SixLabors.ImageSharp.Image originalImage, int newWidth, int newHeight)
	{
		var resizedImage = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(
			coverImageWidth, coverImageHeight);

		resizedImage.Mutate(ctx => ctx.BackgroundColor(SixLabors.ImageSharp.Color.Transparent));
		originalImage.Mutate(ctx => ctx.Resize(newWidth, newHeight));

		var x = (coverImageWidth - newWidth) / 2;
		var y = (coverImageHeight - newHeight) / 2;

		resizedImage.Mutate(ctx => ctx.DrawImage(originalImage, new SixLabors.ImageSharp.Point(x, y), 1f));

		return resizedImage;
	}

	static byte[] SaveImageAsPng(SixLabors.ImageSharp.Image image)
	{
		using var outputStream = new MemoryStream();
		image.SaveAsPng(outputStream, new SixLabors.ImageSharp.Formats.Png.PngEncoder
		{
			CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.DefaultCompression
		});
		return outputStream.ToArray();
	}

	#endregion
}