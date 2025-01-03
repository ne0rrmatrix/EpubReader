using HtmlAgilityPack;
using System.Text;

namespace EpubReader.Service;

public class EpubTextExtractor
{
    public static string ExtractAllText(string htmlContent)
    {
        var plainText = new StringBuilder();
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        var paragraphs = doc.DocumentNode.SelectNodes("//p");

        if (paragraphs != null)
        {
            foreach (var paragraph in paragraphs)
            {
                string text = paragraph.InnerText.Trim();
                // Add the paragraph text
                if (!string.IsNullOrWhiteSpace(text))
                {
                    plainText.AppendLine(text);
                    plainText.AppendLine();
                }
            }
        }
        return plainText.ToString().Trim();
    }
}
