using EpubReader.Models;
using MetroLog;
using Microsoft.Maui.Graphics.Skia;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using VersOne.Epub;
using VersOne.Epub.Options;
using VersOne.Epub.Schema;
using Image = SixLabors.ImageSharp.Image;
using SizeF = Microsoft.Maui.Graphics.SizeF;

namespace EpubReader.Service;

public static partial class EbookService
{
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

		Book books = new()
		{
			Title = book.Title.Trim(),
			Authors = [.. book.AuthorList.Where(author => author is not null).Select(author => new Author { Name = author })],
			FilePath = path,
			CoverImage = book.ReadCover() ?? GenerateCoverImage(book.Title),
			Chapters = GetChapters([.. book.Content.Html.Local], book),
			Images = [.. book.Content.Images.Local.Select(item => GetImage(item.ReadContent(), item.FilePath))],
			Css = [.. book.Content.Css.Local.Select(style => new Css { FileName = Path.GetFileName(style.FilePath), Content = style.ReadContent() })],
		};
		return books;
	}

	static string? GetTitle(EpubBookRef book, EpubLocalTextContentFileRef? item)
	{
		var epub3Nav = book.Schema.Epub3NavDocument?.Navs[0]?.Ol?.Lis?.ToList() ?? [];
		var epub2Nav = book.Schema.Epub2Ncx?.NavMap?.Items ?? [];
		var fileName = Path.GetFileName(item?.FilePath) ?? string.Empty;
		var result = book.Schema.Package.EpubVersion switch
		{
			EpubVersion.EPUB_2 => epub2Nav?.Find(x => x.Content.Source == fileName)?.NavigationLabels[0]?.Text,
			EpubVersion.EPUB_3 => epub3Nav?.Find(x => x.Anchor?.Href == fileName)?.Anchor?.Text,
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
		List<Chapter> chapters =
		[
			.. chaptersRef.Where(item => !item.FilePath.Contains("_split_")).Select(item => new Chapter
			{
				HtmlFile = item.ReadContent(),
				FileName = item.FilePath,
				Title = GetTitle(book, item) ?? string.Empty,
			}),
		];
		if (chaptersRef.FindAll(x => x.FilePath.Contains("_split_001")).Count > 2)
		{
			chapters = [];
			foreach (var item in chaptersRef.Where(x => x is not null).Where(item => item.FilePath.Contains("_split_000")))
			{
				var temp = item.FilePath.Replace("_split_000", "_split_001");
				EpubLocalTextContentFileRef? epubLocalFile = chaptersRef.Find(x => x.FilePath == temp);
				chapters.Add(new Chapter
				{
					HtmlFile = epubLocalFile?.ReadContent() ?? string.Empty,
					FileName = epubLocalFile?.FilePath ?? string.Empty,
					Title = GetTitle(book, item) ?? string.Empty,
				});
			}
			return chapters;
		}
		if (chapters.Count < 3)
		{
			return
			[
				.. chaptersRef.Select(item => new Chapter
				{
					HtmlFile = item.ReadContent(),
					FileName = item.FilePath,
					Title = GetTitle(book, item) ?? string.Empty,
				}),
			];
		}
		return chapters;
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
		Microsoft.Maui.Graphics.Point point = new(
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

	static Models.Image GetImage(byte[] imageByte, string href)
	{
		var quality = 100;
		var size = Convert.ToDouble(imageByte.Length) / 1024;
		if ((size) > 300)
		{
			quality = 50;
		}
		using MemoryStream ms = new(imageByte);
		using Image image = Image.Load(ms);
		using MemoryStream resizedMs = new();
		image.Save(resizedMs, new JpegEncoder { Quality =quality });
		string base64 = Convert.ToBase64String(resizedMs.ToArray());
		return new Models.Image
		{
			FileName = Path.GetFileName(href),
			ImageUrl = base64
		};
	}
}
