using System.Text.Encodings.Web;
using EpubCore;
using EpubReader.Models;
using MetroLog;
using Image = EpubReader.Models.Image;
using Page = EpubReader.Models.Page;

namespace EpubReader.Service;

public partial class EbookService
{
    static readonly ILogger logger = LoggerFactory.GetLogger(nameof(EbookService));
    public EbookService()
    {
    }

    public static Book OpenEbook(string path)
    {
        var book = EpubCore.EpubReader.Read(path);
        var toc = book.TableOfContents.ToList();
        var html = book.Resources.Html.ToList();
		var pageNav = book.Format.Ncx.PageList?.PageTargets.ToList() ?? [];
		var imageList = book.Resources.Images.ToList();

		List<Chapter> chapters = [];
        List<Author> authors = [];
        List<CSS> css = [];
		List<Page> pages = [];
		List<Image> images = [];

		foreach (var item in pageNav)
		{
			Page page = new()
			{
				Id = Int32.Parse(item.Value),
				NavPoint = item.Id ?? string.Empty,
				FileName = StringCleaner.GetPageNumberInfo(item.ContentSrc.Remove(0, 6)) ?? string.Empty,
			};
			pages.Add(page);
		}
		for (int i = 0; i < toc.Count; i++)
        {
            var htmlFile = html.Find(x => x.AbsolutePath == toc[i].AbsolutePath)?.TextContent ?? string.Empty;
			var page = pages.FindAll(x => x.FileName == toc[i].AbsolutePath.Remove(0, 7));
			
			var title = toc[i].Title;
            if(string.IsNullOrEmpty(htmlFile))
            {
                logger.Info("Html file is null");
            }
            var chapter = new Chapter()
            {
                Title = title,
				Pages = page,
				HtmlFile = htmlFile,
                FileName = Path.GetFileName(toc[i].RelativePath),
            };
            chapters.Add(chapter);
        }
       
        foreach (var author in book.Authors)
        {
            if (author is not null)
            {
                authors.Add(new Author { Name = author });
            }
        }

		foreach (var item in imageList)
		{
			var image = GetImage(item.Content, item.Href);
			images.Add(image);
		}
		
		foreach (var style in book.Resources.Css)
        {
            CSS cSS = new()
            {
                FileName = Path.GetFileName(style.FileName),
                Content = style.TextContent
            };
            css.Add(cSS);
        }
		var coverImage = BytesToWebSafeString(book.CoverImage);
		var mimeType = GetMimeType(book.CoverImageHref);
		
		Book books = new()
        {
            Title = book.Title.Trim(),
            Authors = authors,
            FilePath = path,
            CoverImage = book.CoverImage,
			CoverUrl = $"data:{mimeType};charset=utf-8;base64, {coverImage}",
			Chapters = [.. chapters],
			Images = [.. images],
			Css = css,
        };
		return books;
    }

	public static Image GetImage(byte[] imageByte, string href)
	{
		var imageString = BytesToWebSafeString(imageByte);
		var mimeType = GetMimeType(href);
		return new Image
		{
			FileName = href,
			ImageUrl = $"data:{mimeType};charset=utf-8;base64, {imageString}"
		};
	}
	public static string GetMimeType(string fileName)
	{
		var fileExtension = Path.GetExtension(fileName);
		System.Diagnostics.Debug.WriteLine(fileExtension);
		return fileExtension switch
		{
			".jpg" => "image/jpeg",
			".jpeg" => "image/jpeg",
			".png" => "image/png",
			".gif" => "image/gif",
			_ => "image/jpeg"
		};
	}
	public static string BytesToWebSafeString(byte[] data)
	{
		string base64 = Convert.ToBase64String(data);
		return HtmlEncoder.Default.Encode(base64);
	}

	static string GetChapter(int chapterIndex, ICollection<EpubTextFile> html)
    {
        var chapter = html.ElementAt(chapterIndex);
        return chapter.TextContent;
    }
}
