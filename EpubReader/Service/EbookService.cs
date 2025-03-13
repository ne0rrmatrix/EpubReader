using EpubReader.Models;
using MetroLog;
using Microsoft.Maui.Graphics.Skia;
using SixLabors.ImageSharp;
using SkiaSharp;
using VersOne.Epub;
using VersOne.Epub.Options;
using VersOne.Epub.Schema;
using Point = Microsoft.Maui.Graphics.Point;
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
			Chapters = GetChapters([.. book.GetReadingOrder().ToList()], book),
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
		if (chaptersRef.FindAll(x => x.FilePath.Contains("_split_001")).Count > 2)
		{
			return
				[
					.. chaptersRef.Where(x => x is not null).Where(item => item.FilePath.Contains("_split_000")).Select(item => new Chapter
					{
						HtmlFile = ReplaceChapter(chaptersRef, item)?.ReadContent() ?? string.Empty,
						FileName = ReplaceChapter(chaptersRef, item)?.FilePath ?? string.Empty,
						Title = GetTitle(book, item) ?? string.Empty,
					}),
				];
		}

		if (chaptersRef.Where(item => !item.FilePath.Contains("_split_")).ToList().Count < 3)
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
		return
			[
				.. chaptersRef.Where(item => !item.FilePath.Contains("_split_")).Select(item => new Chapter
				{
					HtmlFile = item.ReadContent(),
					FileName = item.FilePath,
					Title = GetTitle(book, item) ?? string.Empty,
				}),
			];
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

	static Models.Image GetImage(byte[] imageByte, string href)
	{
		const int maxSizeKB = 300;
		var sizeKB = imageByte.Length / 1024;

		// Fast path: if image is already small enough, return as-is without any processing
		if (sizeKB < maxSizeKB)
		{
			return new Models.Image
			{
				FileName = Path.GetFileName(href),
				ImageUrl = Convert.ToBase64String(imageByte)
			};
		}

		// Determine quality and quality based on size
		int quality = 75;
		float scale = 1.0f;

		if (sizeKB > 1000)
		{
			scale = 0.5f;
			quality = 60;
		}
		else if (sizeKB > 600)
		{
			scale = 0.7f;
			quality = 70;
		}

		try
		{
			if (OperatingSystem.IsAndroid())
			{
#if ANDROID
				var image = ResizeImageAndroid(imageByte, quality, scale);
				return new Models.Image
				{
					FileName = Path.GetFileName(href),
					ImageUrl = Convert.ToBase64String(image)
				};
#endif
			}

			// For larger images, use the SKData and SKImage approach which is more reliable
			using var skData = SKData.CreateCopy(imageByte);
			using var originalImage = SKImage.FromEncodedData(skData);

			if (originalImage is null)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to process image: {href}");
				// Fallback to original if we can't process the image
				return new Models.Image
				{
					FileName = Path.GetFileName(href),
					ImageUrl = Convert.ToBase64String(imageByte)
				};
			}

			SKImage resultImage;

			if (scale < 1.0f)
			{
				// We need to quality
				var originalBitmap = SKBitmap.FromImage(originalImage);
				int newWidth = (int)(originalBitmap.Width * scale);
				int newHeight = (int)(originalBitmap.Height * scale);

				using var scaledBitmap = new SKBitmap(newWidth, newHeight);
				if (originalBitmap.ScalePixels(scaledBitmap, SKFilterQuality.Medium))
				{
					resultImage = SKImage.FromBitmap(scaledBitmap);
				}
				else
				{
					// If scaling fails, just use the original
					resultImage = originalImage;
				}
			}
			else
			{
				// Just use original dimensions
				resultImage = originalImage;
			}

			// Encode with compression
			using var encodedData = resultImage.Encode(SKEncodedImageFormat.Jpeg, quality);
			byte[] finalImageBytes = encodedData.ToArray();

			return new Models.Image
			{
				FileName = Path.GetFileName(href),
				ImageUrl = Convert.ToBase64String(finalImageBytes)
			};
		}
		catch (Exception)
		{
			// If anything fails, return the original image
			return new Models.Image
			{
				FileName = Path.GetFileName(href),
				ImageUrl = Convert.ToBase64String(imageByte)
			};
		}
	}

#if ANDROID
	static (int width, int height) GetImageDimensions(byte[] imageBytes)
	{
		using (SKBitmap bitmap = SKBitmap.Decode(imageBytes))
		{
			if (bitmap != null)
			{
				return (bitmap.Width, bitmap.Height);
			}
		}

		return (0, 0); // Return zeros if decoding failed
	}
	
	static byte[] ResizeImageAndroid (byte[] imageData, int quality, float scale)
	{
		// Load the bitmap
		Android.Graphics.Bitmap? originalImage = Android.Graphics.BitmapFactory.DecodeByteArray (imageData, 0, imageData.Length);
		Android.Graphics.Bitmap? resizedImage;
		if (scale < 1.0f && originalImage is not null)
		{
			// We need to quality
			int newWidth = (int)(originalImage.Width * scale);
			int newHeight = (int)(originalImage.Height * scale);
			resizedImage = Android.Graphics.Bitmap.CreateScaledBitmap(originalImage, newWidth, newHeight, false);
		}
		else
		{
			// Get the dimensions of the image
			var (width, height) = GetImageDimensions(imageData);
			resizedImage = Android.Graphics.Bitmap.CreateScaledBitmap(originalImage!, width, height, false);
		}
		using MemoryStream ms = new();
		resizedImage?.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, quality, ms);
		return ms.ToArray();
	}
#endif
}
