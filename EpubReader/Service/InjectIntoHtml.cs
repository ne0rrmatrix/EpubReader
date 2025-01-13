﻿using System.Text.RegularExpressions;
using EpubReader.Models;

namespace EpubReader.Service;

public partial class InjectIntoHtml
{
	static readonly string scrollToPageFunction = @"
function scrollToPage(pageNumber) {
    const targetElement = document.getElementById(`page_${pageNumber}`);
    if (targetElement) {
        targetElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
}";

	static readonly string disableScrollBars = @"
function disableScrollBars() {
document.querySelector('body').style.overflow = 'scroll';
var style = document.createElement('style');
style.type = 'text/css';
style.innerHTML = '::-webkit-scrollbar { display: none }';
document.getElementsByTagName('body')[0].appendChild(style);
}";

	static readonly string disableScroll = @"
window.addEventListener('wheel', function(event) {
    event.preventDefault();
}, { passive: false });

window.addEventListener('touchmove', function(event) {
    event.preventDefault();
}, { passive: false });";

	static readonly string getCurrentPageFunction = @"
function getCurrentPage() {
    const pageElements = document.querySelectorAll('[id^=""page_""]');
    let currentPage = 0;
    
    pageElements.forEach((element) => {
        const rect = element.getBoundingClientRect();
        if (rect.top <= window.innerHeight && rect.bottom >= 0) {
            const pageNum = parseInt(element.id.split('_')[1]);
            currentPage = pageNum;
        }
    });
    console.log('Current page: ' + currentPage);
    return currentPage;
}";

	static readonly string scrollCheck = @"
function scrolledToBottom() {
    const scrollPosition = Math.ceil(window.scrollY);
    const viewportHeight = window.innerHeight;
    const totalHeight = document.documentElement.scrollHeight;
    const bottomPosition = totalHeight - viewportHeight;
    
    return scrollPosition >= bottomPosition ? 'Yes' : 'No';
}
";

	static readonly string scrolledToTop = @"
function ScrolledToTop() {
if (window.pageYOffset === 0) {
	return 'Yes';
}
}";

	public static string InjectAllCss(string html, Book book, Settings settings)
    {
		var otherCss = book.Css[^1].Content ?? string.Empty;
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
            // GetMimeType duplicate font-size and font-family in otherCss
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

		var js = scrollCheck + disableScroll + scrolledToTop + getCurrentPageFunction + disableScrollBars + scrollToPageFunction;
		html = InjectJavascript(html, js);
		foreach (var image in book.Images)
		{
			html = ReplaceImageUrls(html, image.FileName, image.ImageUrl);
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
	static string ReplaceImageUrls(string htmlContent, string sourcePattern, string newImageSource)
	{
		// Handle standard img tags
		string imgPattern = $@"<img[^>]*src=[""']([^""']*{sourcePattern}[^""']*)[""'][^>]*>";
		htmlContent = Regex.Replace(htmlContent, imgPattern, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, newImageSource);
		});

		// Handle SVG image tags
		string svgPattern = $@"<image[^>]*xlink:href=[""']([^""']*{sourcePattern}[^""']*)[""'][^>]*>";
		htmlContent = Regex.Replace(htmlContent, svgPattern, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, newImageSource);
		});

		return htmlContent;
	}
	
	static string InjectJavascript(string html, string javascript)
	{
		int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
		if (headEndTagIndex >= 0)
		{
			html = html.Insert(headEndTagIndex, $@"<script>{javascript}</script>");
		}
		else
		{
			// If no <head> tag is found, prepend the style to the HTML
			html = $"<script>{javascript}</script>" + html;
		}
		return html;
	}
}