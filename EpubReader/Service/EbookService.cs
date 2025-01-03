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
        var chapters = book.TableOfContents;
        Chapter[] chapter = new Chapter[chapters.Count];
        for (int i = 0; i < chapters.Count; i++)
        {
            var html = GetChapter(i, book.Resources.Html);
            string plainText = EpubTextExtractor.ExtractAllText(html);
            chapter[i] = new Chapter()
            {
                Title = chapters[i].Title,
                HtmlFile = html,
                PlainText = plainText
            };
        }
       
        List<Author> authors = [];
        foreach (var author in book.Authors)
        {
            if(author is not null)
            {
                authors.Add(new Author { Name = author});
            }
        }

        Book books = new()
        {
            Title = book.Title.Trim(),
            Authors = authors,
            CoverImage = book.CoverImage,
            CoverImageFileName = book.CoverImageHref,
            Chapters = [.. chapter]
        };
        return books;
    }
    static string GetChapter(int chapterIndex, ICollection<EpubTextFile> html)
    {
        var chapter = html.ElementAt(chapterIndex);
        return chapter.TextContent;
    }
}
