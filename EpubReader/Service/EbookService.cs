using EpubReader.Models;
using MetroLog;
using Microsoft.Maui.Graphics.Skia;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using SizeF = Microsoft.Maui.Graphics.SizeF;
using VersOne.Epub;

namespace EpubReader.Service;

public partial class EbookService
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(EbookService));
	protected EbookService()
	{
	}

	public static Book? OpenEbook(string path)
	{

		List<Chapter> chapters = [];
		List<Author> authors = [];
		List<Css> css = [];
		List<Models.Image> images = [];
		
		EpubBook book;

		try
		{
			book = VersOne.Epub.EpubReader.ReadBook(path);
		}
		catch (Exception ex)
		{
			logger.Error($"Error opening ebook: {ex.Message}");
			return null;
		}
		var epub3Nav = book.Schema.Epub3NavDocument?.Navs[0]?.Ol?.Lis?.ToList();
		var epub2Nav = book.Schema.Epub2Ncx?.NavMap?.Items;
		foreach (var item in book.Content.Html.Local)
		{
			var chapter = new Chapter
			{
				HtmlFile = item.Content,
				FileName = item.FilePath,
				Title = book.Navigation?.Find(x => x.Link?.ContentFilePath == item.FilePath)?.Title ??
				epub2Nav?.Find(x => x.Content.Source == Path.GetFileName(item.FilePath))?.NavigationLabels[0]?.Text ??
				epub3Nav?.Find(x => x.Anchor?.Href == Path.GetFileName(item.FilePath))?.Anchor?.Text ??string.Empty,
			};
			chapters.Add(chapter);
		}
		
		authors.AddRange(book.AuthorList.Where(author => author is not null).Select(author => new Author { Name = author }));
		images.AddRange(book.Content.Images.Local.Select(item => GetImage(ResizeImage(item.Content, 80), item.FilePath)));
		css.AddRange(book.Content.Css.Local.Select(style => new Css { FileName = Path.GetFileName(style.FilePath), Content = style.Content }));

		Book books = new()
		{
			Title = book.Title.Trim(),
			Authors = authors,
			FilePath = path,
			CoverImage = book.CoverImage ?? GenerateCoverImage(book.Title),
			Chapters = [.. chapters],
			Images = [.. images],
			Css = css,
		};
		return books;
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
		string base64 = Convert.ToBase64String(imageByte);
		return new Models.Image
		{
			FileName = Path.GetFileName(href),
			ImageUrl = base64
		};
	}

	static byte[] ResizeImage(byte[] imageData, int quality)
	{
		// If the image is smaller than 500 bytes, return it as is
		if (imageData.Length < 900000)
		{
			return imageData;
		}
		using MemoryStream ms = new(imageData);
		using Image image = Image.Load(ms);
		using MemoryStream resizedMs = new();
		image.Save(resizedMs, new JpegEncoder { Quality = quality });
		return resizedMs.ToArray();
	}
}
