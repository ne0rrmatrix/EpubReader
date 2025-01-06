using System.Text.RegularExpressions;
using EpubReader.Models;

namespace EpubReader.Service;

public partial class CssInjector(Settings settings, string otherCss)
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

        if (!string.IsNullOrEmpty(settings.BackgroundColor) || !string.IsNullOrEmpty(settings.TextColor) || settings.FontSize > 0 || !string.IsNullOrEmpty(settings.FontFamily))
        {
            styleTag = $@"
    body {{
        {(string.IsNullOrEmpty(settings.BackgroundColor) ? "" : $"background-color: {settings.BackgroundColor};")}
        {(string.IsNullOrEmpty(settings.TextColor) ? "" : $"color: {settings.TextColor};")}
        {(settings.FontSize > 0 ? $"font-size: {settings.FontSize}px !important;" : "")}
        {(string.IsNullOrEmpty(settings.FontFamily) ? "" : $"font-family: {settings.FontFamily};")}
    }}";
        }

        // Combine the style tag with other CSS
        if (!string.IsNullOrEmpty(otherCss))
        {
            // Replace duplicate font-size and font-family in otherCss
            if (settings.FontSize > 0)
            {
                otherCss = FontSizeRegex().Replace(otherCss, string.Empty);
            }
            if (!string.IsNullOrEmpty(settings.FontFamily))
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

    static string RemoveStyleTags(string html)
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
