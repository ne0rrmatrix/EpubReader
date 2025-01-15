using System.Text.Encodings.Web;
using EpubReader.Models;
using MetroLog;
using Image = EpubReader.Models.Image;

namespace EpubReader.Service;

public partial class EbookService
{
    static readonly ILogger logger = LoggerFactory.GetLogger(nameof(EbookService));
    protected EbookService()
    {
    }

    public static Book OpenEbook(string path)
    {
        var book = EpubCore.EpubReader.Read(path);
        var toc = book.TableOfContents.ToList();
        var html = book.Resources.Html.ToList();
		var imageList = book.Resources.Images.ToList();

		List<Chapter> chapters = [];
        List<Author> authors = [];
        List<Css> css = [];
		List<Image> images = [];

		for (int i = 0; i < toc.Count; i++)
        {
            var htmlFile = html.Find(x => x.AbsolutePath == toc[i].AbsolutePath)?.TextContent ?? string.Empty;
			var title = toc[i].Title;
            if(string.IsNullOrEmpty(htmlFile))
            {
                logger.Info("Html file is null");
            }
            var chapter = new Chapter()
            {
                Title = title,
				HtmlFile = htmlFile,
                FileName = Path.GetFileName(toc[i].RelativePath),
            };
            chapters.Add(chapter);
        }
		authors.AddRange(book.Authors.Where(author => author is not null).Select(author => new Author { Name = author }));
		foreach (var item in imageList)
		{
			var image = GetImage(item.Content, item.Href);
			images.Add(image);
		}
		
		foreach (var style in book.Resources.Css)
        {
            Css cSS = new()
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
			HasPages = book.Format.Ncx.PageList?.PageTargets is not null && book.Format.Ncx.PageList.PageTargets.Count > 0
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
}
