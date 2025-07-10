using System.Text;
using EpubReader.Models;
using EpubReader.Util;
using MetroLog;
using Microsoft.Maui.Graphics.Skia;
using SixLabors.ImageSharp;
using VersOne.Epub;
using VersOne.Epub.Options;
using VersOne.Epub.Schema;
using Point = Microsoft.Maui.Graphics.Point;
using SizeF = Microsoft.Maui.Graphics.SizeF;

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
	static readonly List<string> jsImports =
		[
			"Container.js",
		];

	static readonly List<string> requiredFiles =
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

	static readonly EpubReaderOptions options = new()
	{
		BookCoverReaderOptions = new()
		{
			Epub2MetadataIgnoreMissingManifestItem = true
		},
		Epub2NcxReaderOptions = new()
		{
			IgnoreMissingContentForNavigationPoints = true
		},
		PackageReaderOptions = new()
		{
			IgnoreMissingToc = true
		},
		SpineReaderOptions = new()
		{
			IgnoreMissingManifestItems = true
		},
		XmlReaderOptions = new()
		{
			SkipXmlHeaders = true
		},
	};

	/// <summary>
	/// Retrieves a book listing from the specified file path.
	/// </summary>
	/// <remarks>This method attempts to open an ePub book from the given path and extract its title and cover
	/// image. If the cover image is missing, a default image is generated based on the book's title.</remarks>
	/// <param name="path">The file path to the ePub book. Must be a valid path to an ePub file.</param>
	/// <returns>A <see cref="Book"/> object containing the title, file path, and cover image of the book. Returns <see
	/// langword="null"/> if the book cannot be opened or an error occurs.</returns>
	public static Book? GetListing(string path)
	{
		EpubBookRef book;
		options.ContentReaderOptions.ContentFileMissing += (sender, e) => e.SuppressException = true;
		try
		{
			book = VersOne.Epub.EpubReader.OpenBook(path, options);
		}
		catch (Exception ex)
		{
			logger.Error($"Get Listing Error: {ex.Message}");
			return null;
		}

		return new Book
		{
			Title = book.Title,
			FilePath = path,
			CoverImage = book.ReadCover() ?? GenerateCoverImage(book.Title),
		};
	}

	/// <summary>
	/// Retrieves a <see cref="Book"/> object from the specified EPUB file stream.
	/// </summary>
	/// <remarks>This method attempts to read an EPUB book from the provided stream. If the EPUB file is missing any
	/// content files, the method suppresses exceptions related to those missing files.</remarks>
	/// <param name="stream">The input stream containing the EPUB file data. Must not be null.</param>
	/// <param name="path">The file path associated with the EPUB file, used for setting the <see cref="Book.FilePath"/> property.</param>
	/// <returns>A <see cref="Book"/> object containing the title, file path, and cover image of the EPUB book. Returns <see
	/// langword="null"/> if the EPUB file cannot be opened or processed.</returns>
	public static Book? GetListing(Stream stream, string path)
	{
		EpubBookRef book;
		options.ContentReaderOptions.ContentFileMissing += (sender, e) => e.SuppressException = true;
		try
		{
			book = VersOne.Epub.EpubReader.OpenBook(stream, options);
		}
		catch (Exception ex)
		{
			logger.Error($"Get Listing Error: {ex.Message}");
			return null;
		}

		return new Book
		{
			Title = book.Title,
			FilePath = path,
			CoverImage = book.ReadCover() ?? GenerateCoverImage(book.Title),
		};
	}

	/// <summary>
	/// Asynchronously opens an eBook from the specified file path and returns a <see cref="Book"/> object representing the
	/// eBook's content.
	/// </summary>
	/// <remarks>This method handles missing content files by suppressing exceptions related to them. It logs any
	/// errors encountered during the opening process. The returned <see cref="Book"/> object includes the eBook's title,
	/// authors, description, cover image, chapters, images, fonts, and CSS files.</remarks>
	/// <param name="path">The file path to the eBook to be opened. Must be a valid path to an ePub file.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Book"/> object with the
	/// eBook's metadata, content, and resources. Returns <see langword="null"/> if the eBook cannot be opened.</returns>
	public static async Task<Book?> OpenEbookAsync(string path)
	{
		options.ContentReaderOptions.ContentFileMissing += (sender, e) => e.SuppressException = true;
		EpubBookRef book;
		try
		{
			book = await VersOne.Epub.EpubReader.OpenBookAsync(path, options);
		}
		catch (Exception ex)
		{
			logger.Error($"Error opening ebook: {ex.Message}");
			return null;
		}

		var list = new List<SharedEpubFiles>();
		foreach (var item in requiredFiles)
		{
			var sharedFile = await GetSharedFilesAsync(item);
			if (sharedFile is not null)
			{
				list.Add(sharedFile);
			}
		}

		List<EpubFonts> fonts = [];
		foreach (var item in book.Content.AllFiles.Local
			.Where(item => item.FilePath
			.Contains(".ttf", StringComparison.InvariantCultureIgnoreCase) || 
				item.FilePath.Contains(".otf", StringComparison.InvariantCultureIgnoreCase) || 
				item.FilePath.Contains(".woff", StringComparison.InvariantCultureIgnoreCase) || 
				item.FilePath.Contains(".woff2", StringComparison.InvariantCultureIgnoreCase)))
			{
			EpubFonts Font = new()
			{
				Content = await item.ReadContentAsBytesAsync(),
				FileName = Path.GetFileName(item.FilePath),
				FontFamily = Path.GetFileNameWithoutExtension(item.FilePath)
			};
			fonts.Add(Font);
		}

		var description = book.Description ?? string.Empty;
		if (!description.Contains("<html>"))
		{
			description = description.Insert(0, "<html><body>");
			description = description.Insert(description.Length, "</body></html>");
		}
		var coverImage = await book.ReadCoverAsync() ?? GenerateCoverImage(book.Title);
		var cssFiles = book.Content.AllFiles.Local
			.Where(item => item.FilePath.EndsWith(".css", StringComparison.InvariantCultureIgnoreCase))
			.Select(item => new Css
			{
				FileName = Path.GetFileName(item.FilePath),
				Content = ProcessCssFiles(item.ReadContentAsText()),
			}).ToList();
		Book books = new()
		{
			Title = book.Title.Trim(),
			Authors = [.. book.AuthorList.Where(author => author is not null).Select(author => new Author { Name = author })],
			FilePath = path,
			Files = list,
			Fonts = fonts,
			Description = description,
			CoverImage = coverImage,
			Chapters = GetChapters([.. await book.GetReadingOrderAsync()], book),
			Images = [.. book.Content.Images.Local.Select(image => GetImage(image.ReadContentAsBytes(), Path.GetFileName(image.FilePath)))],
			Css = cssFiles,
		};
		return books;
	}

	static string ProcessCssFiles(string cssFile)
	{
		if (string.IsNullOrEmpty(cssFile))
		{
			return string.Empty;
		}
		cssFile = HtmlAgilityPackExtensions.RemoveCalibreReferences(cssFile);
		cssFile =FilePathExtensions.SetFontFilenames(cssFile);
		cssFile = HtmlAgilityPackExtensions.UpdateImagePathsForCSSFiles(cssFile);
		return cssFile;
	}
	static async Task<SharedEpubFiles?> GetSharedFilesAsync(string fileName)
	{
		var exists = await FileSystem.AppPackageFileExistsAsync(fileName);
		if (!exists)
		{
			return null;
		}
		var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
		var reader = new StreamReader(stream);
		var memoryStream = new MemoryStream();
		await reader.BaseStream.CopyToAsync(memoryStream);
		var bytes = memoryStream.ToArray();
		var sharedFile = new SharedEpubFiles();
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
		sharedFile.FileName = fileName;
		return sharedFile;
	}

	static string? GetTitle(EpubBookRef book, EpubLocalTextContentFileRef? item)
	{
		var epub3Nav = book.Schema.Epub3NavDocument?.Navs[0]?.Ol?.Lis?.ToList() ?? [];
		var epub2Nav = book.Schema.Epub2Ncx?.NavMap?.Items ?? [];
		var fileName = Path.GetFileName(item?.FilePath) ?? string.Empty;
		var result = book.Schema.Package.EpubVersion switch
		{
			EpubVersion.EPUB_2 => epub2Nav.Find(x => x.Content.Source == fileName)?.NavigationLabels[0]?.Text ?? epub2Nav.Find(x => Path.GetFileName(x.Content.Source?.Split('#')[0]) == fileName)?.NavigationLabels[0]?.Text,
			EpubVersion.EPUB_3 => epub3Nav.Find(x => x.Anchor?.Href == fileName)?.Anchor?.Text ?? epub3Nav.Find(x => Path.GetFileName(x.Anchor?.Href)?.Split('#')[0] == fileName)?.Anchor?.Text,
			EpubVersion.EPUB_3_1 => epub3Nav.Find(x => x.Anchor?.Href == fileName)?.Anchor?.Text,
			_ => book.GetNavigation()?.Find(x => x.Link?.ContentFilePath == item?.FilePath)?.Title,
		};

		if (result is null)
		{
			return book.Schema.Epub2Ncx?.NavMap?.Items?.Find(x => Path.GetFileName(x.Content.Source) == fileName)?.NavigationLabels[0].Text;
		}
		return result;
	}

	static List<Chapter> GetChapters(List<EpubLocalTextContentFileRef> chaptersRef, EpubBookRef book)
	{
		var chapters = new List<Chapter>();

		if (chaptersRef.FindAll(x => x.FilePath.Contains("_split_001")).Count > 2)
		{
			foreach (var item in chaptersRef.Where(x => x is not null).Where(item => item.FilePath.Contains("_split_000")))
			{
				var temp = ReplaceChapter(chaptersRef, item);
				var htmlFile = temp?.ReadContent() ?? string.Empty;
				if(string.IsNullOrEmpty(htmlFile))
				{
					continue;
				}
				var chapter = GetChapter(book, htmlFile, item);
				if (string.IsNullOrEmpty(chapter.Title))
				{
					chapter.Title = GetTitle(book, temp) ?? string.Empty;
				}
				chapters.Add(chapter);
			}
			return chapters;
		}

		if (chaptersRef.Where(item => !item.FilePath.Contains("_split_")).ToList().Count < 3)
		{
			foreach (var item in chaptersRef)
			{
				var htmlFile = item.ReadContent();
				chapters.Add(GetChapter(book, htmlFile, item));
			}
			return chapters;
		}

		foreach (var item in chaptersRef.Where(item => !item.FilePath.Contains("_split_")))
		{
			var htmlFile = item.ReadContent();
			chapters.Add(GetChapter(book, htmlFile, item));
		}
		return chapters;
	}

	static Chapter GetChapter(EpubBookRef book, string htmlFile, EpubLocalTextContentFileRef item)
	{
		htmlFile = ProcessHtml(htmlFile);
		var fileName = Path.GetFileName(item.FilePath);
		var title = GetTitle(book, item) ?? string.Empty;
		return new Chapter
		{
			HtmlFile = htmlFile ?? string.Empty,
			FileName = fileName,
			Title = title,
		};
	}

	static EpubLocalTextContentFileRef? ReplaceChapter(List<EpubLocalTextContentFileRef> chaptersRef, EpubLocalContentFileRef item)
	{
		var temp = item.FilePath.Replace("_split_000", "_split_001");
		return chaptersRef.Find(x => x.FilePath == temp);
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

	static byte[] GenerateCoverImage(string title)
	{
		SkiaBitmapExportContext bmp = new(200, 400, 1.0f);
		ICanvas canvas = bmp.Canvas;

		Rect backgroundRectangle = new(0, 0, bmp.Width, bmp.Height);
		canvas.FillColor = Colors.White;
		canvas.FillRectangle(backgroundRectangle);
		canvas.StrokeColor = Colors.Black;
		canvas.StrokeSize = 28;
		canvas.DrawRectangle(backgroundRectangle);

		Microsoft.Maui.Graphics.Font font = new("Arial");
		float fontSize = 24;
		canvas.FontSize = fontSize;
		SizeF textSize = canvas.GetStringSize(title, font, fontSize);

		// Draw a rectangle to hold the string
		Point point = new(
			x: (bmp.Width - textSize.Width) / 2,
			y: (bmp.Height - textSize.Height) / 2);
		Rect myTextRectangle = new(point, textSize);
		canvas.FillColor = Colors.Black.WithAlpha(.5f);
		canvas.FillRectangle(myTextRectangle);
		canvas.StrokeSize = 2;
		canvas.StrokeColor = Colors.Yellow;
		canvas.DrawRectangle(myTextRectangle);

		// Daw the string itself
		canvas.FontSize = fontSize * .9f; // smaller than the rectangle
		canvas.FontColor = Colors.White;
		canvas.DrawString(title, myTextRectangle,
			HorizontalAlignment.Center, VerticalAlignment.Center, TextFlow.OverflowBounds);
		return bmp.Image.AsBytes(ImageFormat.Jpeg);
	}

	static Models.Image GetImage(byte[] imageByte, string fileName)
	{
		return new Models.Image
		{
			FileName = fileName,
			Content = imageByte,
		};
	}
}
