using System.Text;

namespace EpubReader.Service;

public partial class TextPaginator(double width, double height)
{
    public List<string> Pages { get; private set; } = [];
    private readonly double pageWidth = width;
    private readonly double pageHeight = height;
    private static readonly string[] separator = ["\n\n"];

    public void PaginateText(string text, Label label)
    {
        Pages.Clear();
        var paragraphs = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        var currentPage = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            label.Text = currentPage.ToString() + paragraph + "\n\n";
           
            var size = label.Measure(pageWidth, double.PositiveInfinity);
            
            if (size.Height > pageHeight && currentPage.Length > 0)
            {
                Pages.Add(currentPage.ToString().Trim());
                currentPage.Clear();
            }

            currentPage.Append(paragraph + "\n\n");
        }

        if (currentPage.Length > 0)
        {
            Pages.Add(currentPage.ToString().Trim());
        }
    }
}
