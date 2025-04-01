using System.Text;
using EpubReader.Models;
using EpubReader.Util;
using HtmlAgilityPack;
using MetroLog;
using Microsoft.Maui.Graphics.Skia;
using SixLabors.ImageSharp;
using VersOne.Epub;
using VersOne.Epub.Options;
using VersOne.Epub.Schema;
using Point = Microsoft.Maui.Graphics.Point;
using SizeF = Microsoft.Maui.Graphics.SizeF;

namespace EpubReader.Service;

public static partial class EbookService
{
	static string wWWpath = string.Empty;
	static readonly List<string> cssImports =
		[
			"ReadiumCSS-before.css",
			"ReadiumCSS-after.css",
			"ReadiumCSS-config.css"
		];

	static readonly List<string> requiredFiles =
		[
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

	public static Book? OpenEbook(string path)
	{
		options.ContentReaderOptions.ContentFileMissing += (sender, e) => e.SuppressException = true;
		EpubBookRef book;
		try
		{
			book = VersOne.Epub.EpubReader.OpenBook(path, options);
		}
		catch (Exception ex)
		{
			logger.Error($"Error opening ebook: {ex.Message}");
			return null;
		}
		var file = FileService.ValidateAndFixFileName(Path.GetFileNameWithoutExtension(book.Title.Trim()));
		wWWpath = FileService.ValidateAndFixDirectoryName(Path.Combine(FileService.WWWDirectory, file));
		Directory.CreateDirectory(wWWpath);
	    var list = new List<SharedEpubFiles>();
		requiredFiles.ForEach(async file =>
		{
			var sharedFile = await GetSharedFiles(file);
			if (sharedFile is not null)
			{
				list.Add(sharedFile);
			}
		});
		List<EpubFonts> fonts = [];
		foreach (var item in book.Content.AllFiles.Local.ToList())
		{
			if (item.FilePath.Contains(".TTF") || item.FilePath.Contains(".OTF") || item.FilePath.Contains(".WOFF") || item.FilePath.Contains(".woff") || item.FilePath.Contains(".ttf") || item.FilePath.Contains(".otf"))
			{
				EpubFonts Font = new()
				{
					Content = item.ReadContentAsBytes(),
					FileName = Path.GetFileName(item.FilePath),
					FontFamily = Path.GetFileNameWithoutExtension(item.FilePath)
				};
				fonts.Add(Font);
			}
		}
		Book books = new()
		{
			Title = book.Title.Trim(),
			Authors = [.. book.AuthorList.Where(author => author is not null).Select(author => new Author { Name = author })],
			FilePath = path,
			Files = list,
			Fonts = fonts,
			CoverImage = book.ReadCover() ?? GenerateCoverImage(book.Title),
			WWWPath = wWWpath,
			Chapters = GetChapters([.. book.GetReadingOrder()], book),
			Images = [.. book.Content.Images.Local.Select(image => GetImage(image.ReadContentAsBytes(), Path.GetFileName(image.FilePath)))],
			Css = [.. book.Content.Css.Local.Select(style => new Css { FileName = Path.GetFileName(style.FilePath), Content = FilePathExtensions.SetFontFilenames(HtmlAgilityPackExtensions.UpdateImagePathsForCSSFiles(style.ReadContent())) })],
		};
		return books;
	}
	static async Task<SharedEpubFiles?> GetSharedFiles(string fileName)
	{
		var exists = await FileSystem.AppPackageFileExistsAsync(fileName);
		if(!exists)
		{
			return null;
		}
		var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
		var reader = new StreamReader(stream);
		var memoryStream = new MemoryStream();
		await reader.BaseStream.CopyToAsync(memoryStream);
		var bytes = memoryStream.ToArray();
		var htmlFile = Encoding.UTF8.GetString(bytes);
		return new SharedEpubFiles
		{
			FileName = Path.GetFileName(fileName),
			HTMLContent = htmlFile,
		};
	}

	static string? GetTitle(EpubBookRef book, EpubLocalTextContentFileRef? item)
	{
		var epub3Nav = book.Schema.Epub3NavDocument?.Navs[0]?.Ol?.Lis?.ToList() ?? [];
		var epub2Nav = book.Schema.Epub2Ncx?.NavMap?.Items ?? [];
		var fileName = Path.GetFileName(item?.FilePath) ?? string.Empty;
		var result = book.Schema.Package.EpubVersion switch
		{
			EpubVersion.EPUB_2 => epub2Nav?.Find(x => x.Content.Source == fileName)?.NavigationLabels[0]?.Text ?? epub2Nav?.Find(x => Path.GetFileName(x.Content.Source?.Split('#')[0]) == fileName)?.NavigationLabels[0]?.Text,
			EpubVersion.EPUB_3 => epub3Nav?.Find(x => x.Anchor?.Href == fileName)?.Anchor?.Text ?? epub3Nav?.Find(x => Path.GetFileName(x.Anchor?.Href)?.Split('#')[0] == fileName)?.Anchor?.Text,
			EpubVersion.EPUB_3_1 => epub3Nav?.Find(x => x.Anchor?.Href == fileName)?.Anchor?.Text,
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
		var doc = new HtmlDocument();
		if (chaptersRef.FindAll(x => x.FilePath.Contains("_split_001")).Count > 2)
		{
			foreach (var item in chaptersRef.Where(x => x is not null).Where(item => item.FilePath.Contains("_split_000")))
			{
				var htmlFile = ReplaceChapter(chaptersRef, item)?.ReadContent() ?? string.Empty;
				doc.LoadHtml(htmlFile);
				htmlFile = ProcessHtml(htmlFile, doc);
				var fileName =  Path.GetFileName(item.FilePath);
				var title = GetTitle(book, item) ?? string.Empty;
				
				chapters.Add(new Chapter
				{
					HtmlFile = htmlFile ?? string.Empty,
					FileName = fileName,
					Title = title,
				});
			}
			return chapters;
		}
		
		if (chaptersRef.Where(item => !item.FilePath.Contains("_split_")).ToList().Count < 3)
		{
			foreach (var item in chaptersRef)
			{
				var htmlFile = item.ReadContent();
				doc.LoadHtml(htmlFile);
				htmlFile = ProcessHtml(htmlFile, doc);
				var fileName = Path.GetFileName(item.FilePath);
				var title = GetTitle(book, item) ?? string.Empty;
				chapters.Add(new Chapter
				{
					HtmlFile = htmlFile ?? string.Empty,
					FileName = fileName,
					Title = title,
				});
			}
			return chapters;
		}
		foreach (var item in chaptersRef.Where(item => !item.FilePath.Contains("_split_")))
		{
			var htmlFile = item.ReadContent();
			doc.LoadHtml(htmlFile);
			htmlFile = ProcessHtml(htmlFile, doc);
			var fileName = Path.GetFileName(item.FilePath);

			var title = GetTitle(book, item) ?? string.Empty;
			chapters.Add(new Chapter
			{
				HtmlFile = htmlFile ?? string.Empty,
				FileName = fileName,
				Title = title,
			});
		}
		return chapters;
	}

	static string ProcessHtml(string htmlFile, HtmlDocument doc)
	{
		htmlFile = HtmlAgilityPackExtensions.UpdateImageUrl(htmlFile);
		htmlFile = HtmlAgilityPackExtensions.RemoveKoboHacks(htmlFile);
		doc.LoadHtml(htmlFile);
		var cssFiles = HtmlAgilityPackExtensions.GetCssFiles(doc);
		htmlFile = HtmlAgilityPackExtensions.RemoveCssLinks(htmlFile);
		htmlFile = HtmlAgilityPackExtensions.AddCssLinks(htmlFile, cssFiles);
		htmlFile = HtmlAgilityPackExtensions.AddCssLinks(htmlFile, cssImports);
		htmlFile = FilePathExtensions.UpdateImagePathsToFilenames(htmlFile);
		htmlFile = FilePathExtensions.UpdateSvgLinks(htmlFile);
		return htmlFile;
	}
	static EpubLocalTextContentFileRef? ReplaceChapter(List<EpubLocalTextContentFileRef> chaptersRef, EpubLocalContentFileRef item)
	{
		var temp = item.FilePath.Replace("_split_000", "_split_001");
		return chaptersRef.Find(x => x.FilePath == temp);
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
		float fontSize = 60;
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
