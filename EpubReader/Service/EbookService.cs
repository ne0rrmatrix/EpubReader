using EpubReader.Models;
using MetroLog;
using Microsoft.Maui.Graphics.Skia;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using SizeF = Microsoft.Maui.Graphics.SizeF;

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

		EpubCore.EpubBook book;

		try
		{
			book = EpubCore.EpubReader.Read(path);
		}
		catch (Exception ex)
		{
			logger.Error($"Error opening ebook: {ex.Message}");
			return null;
		}
		
		var navMap = book.Format?.Ncx?.NavMap ?? new();
		var toc = book.TableOfContents.ToList();
		var html = book.Resources.Html.ToList();
		var imageList = book.Resources.Images.ToList();
		var imageItem = imageList.MaxBy(x => x.Content.Length);
	
		foreach (var navPoint in navMap.NavPoints)
		{	
			if (navPoint.ContentSrc is null)
			{
				continue;
			}
			var chapter = html.Find(x => x.Href == navPoint.ContentSrc) ?? html.Find(x => navPoint.ContentSrc.Contains(x.Href));
			if (chapter is not null && chapter.AbsolutePath.Contains("_split_000.xhtml"))
			{
				chapter = html.Find(x => x.AbsolutePath == chapter.AbsolutePath.Replace("_split_000.xhtml", "_split_001.xhtml"));
			}
			
			if (chapter is not null)
			{
				chapters.Add(new Chapter
				{
					Title = navPoint.NavLabelText ?? string.Empty,
					HtmlFile = chapter.TextContent ?? string.Empty,
					FileName = chapter.FileName ?? string.Empty
				});
			}
		}
		
		if (navMap.NavPoints.Count <= 1)
		{
			chapters.AddRange(html.Select(chapter => new Chapter
			{
				Title = toc.Find(x => x.AbsolutePath == chapter.AbsolutePath)?.Title ?? string.Empty,
				HtmlFile = chapter.TextContent ?? string.Empty,
				FileName = chapter.FileName ?? string.Empty
			}));
		}
		
		authors.AddRange(book.Authors.Where(author => author is not null).Select(author => new Author { Name = author }));
		images.AddRange(imageList.Select(item => GetImage(ResizeImage(item.Content, 1080, 1920, 80), item.Href)));
		css.AddRange(book.Resources.Css.Select(style => new Css { FileName = Path.GetFileName(style.FileName), Content = style.TextContent }));

		Book books = new()
		{
			Title = book.Title.Trim(),
			Authors = authors,
			FilePath = path,
			CoverImage = book.CoverImage ?? imageItem?.Content ?? GenerateCoverImage(book.Title),
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
			FileName = href,
			ImageUrl = base64
		};
	}

	static byte[] ResizeImage(byte[] imageData, int maxWidth, int maxHeight, int quality)
	{
		// If the image is smaller than 500 bytes, return it as is
		if (imageData.Length < 900000)
		{
			return imageData;
		}
		using MemoryStream ms = new(imageData);
		using Image image = Image.Load(ms);
		using MemoryStream resizedMs = new();
		image.Mutate(x => x.Resize(maxWidth, maxHeight));
		image.Save(resizedMs, new JpegEncoder { Quality = quality });
		return resizedMs.ToArray();
	}
}
