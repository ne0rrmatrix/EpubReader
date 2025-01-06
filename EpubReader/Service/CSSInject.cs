using System.Text.RegularExpressions;

namespace EpubReader.Service;

public partial class CssInjector(string backgroundColor, string textColor, int fontSize, string fontFamily, string otherCss)
{
    public string InjectAllCss(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new ArgumentException("HTML content cannot be null or empty.", nameof(html));
        }

        // Remove existing <style> tags
        html = RemoveStyleTags(html);

        // Construct the style tag with additional CSS
        string styleTag = string.Empty;

        if (!string.IsNullOrEmpty(backgroundColor) || !string.IsNullOrEmpty(textColor) || fontSize > 0 || !string.IsNullOrEmpty(fontFamily))
        {
            styleTag = $@"
    body {{
        {(string.IsNullOrEmpty(backgroundColor) ? "" : $"background-color: {backgroundColor};")}
        {(string.IsNullOrEmpty(textColor) ? "" : $"color: {textColor};")}
        {(fontSize > 0 ? $"font-size: {fontSize}px !important;" : "")}
        {(string.IsNullOrEmpty(fontFamily) ? "" : $"font-family: '{fontFamily}', sans-serif !important;")}
    }}";
        }

        // Combine the style tag with other CSS
        if (!string.IsNullOrEmpty(otherCss))
        {
            // Replace duplicate font-size and font-family in otherCss
            if (fontSize > 0)
            {
                otherCss = FontSizeRegex().Replace(otherCss, string.Empty);
            }
            if (!string.IsNullOrEmpty(fontFamily))
            {
                otherCss = FontFamilyRegex().Replace(otherCss, string.Empty);
            }

            styleTag += otherCss;
        }

        // Inject the combined CSS into the HTML
        if (!string.IsNullOrEmpty(styleTag))
        {
            html = InjectCss(html, styleTag);
        }

        return html;
    }

    [GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline)]
    private static partial Regex StyleTagRegex();

    [GeneratedRegex("font-size:\\s*\\d+px\\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex FontSizeRegex();

    [GeneratedRegex("font-family:\\s*[^;]+?\\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex FontFamilyRegex();

    private static string RemoveStyleTags(string html)
    {
        return StyleTagRegex().Replace(html, string.Empty);
    }

    static string InjectCss(string html, string css)
    {
        // Assuming you want to inject the CSS into the <head> section
        int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headEndTagIndex >= 0)
        {
            html = html.Insert(headEndTagIndex, $"<style>{css}</style>");
        }
        else
        {
            // If no <head> tag is found, prepend the style to the HTML
            html = $"<style>{css}</style>" + html;
        }

        return html;
    }
}
