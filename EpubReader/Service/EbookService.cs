using EpubCore;
using EpubReader.Models;
using MetroLog;

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

        List<Chapter> chapters = [];
        List<Author> authors = [];
        List<CSS> css = [];

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
       
        foreach (var author in book.Authors)
        {
            if (author is not null)
            {
                authors.Add(new Author { Name = author });
            }
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

        Book books = new()
        {
            Title = book.Title.Trim(),
            Authors = authors,
            FilePath = path,
            CoverImage = book.CoverImage,
            Chapters = [.. chapters],
            Css = css,
        };
        return books;
    }

    static string GetChapter(int chapterIndex, ICollection<EpubTextFile> html)
    {
        var chapter = html.ElementAt(chapterIndex);
        return chapter.TextContent;
    }
}
