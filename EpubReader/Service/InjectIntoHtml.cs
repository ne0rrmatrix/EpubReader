﻿using System.Text.RegularExpressions;
using EpubReader.Models;

namespace EpubReader.Service;

public static partial class InjectIntoHtml
{
	static readonly string getScrollPosition = @"
function getVerticalScroll() {
    return window.scrollY || window.pageYOffset;
}
";
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

	static string GenerateCSSFromString(string html, Settings settings)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			throw new ArgumentException("HTML content cannot be null or empty.", nameof(html));
		}

		// Construct the style tag with additional Css
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
		return styleTag;
	}

	static string FilterCss(string css, Settings settings)
	{
		if (string.IsNullOrEmpty(css))
		{
			return string.Empty;
		}
		// Remove font-size and font-family from the Css
		if (settings.FontSize > 0)
		{
			css = FontSizeRegex().Replace(css, string.Empty);
		}
		if (!string.IsNullOrEmpty(settings.FontFamily))
		{
			css = FontFamilyRegex().Replace(css, string.Empty);
		}
		return css;
	}
	public static string InjectAllCss(string html, Book book, Settings settings)
	{
		// Remove existing <style> tags
		html = RemoveStyleTags(html);

		var otherCss = book.Css[^1].Content ?? string.Empty;
		string styleTag = GenerateCSSFromString(html, settings);

		otherCss = FilterCss(otherCss, settings);
		styleTag += otherCss;


		// Inject the combined Css into the HTML
		if (!string.IsNullOrEmpty(styleTag))
		{
			html = InjectCss(html, styleTag);
		}

		var js = scrollCheck + disableScroll + scrolledToTop + getCurrentPageFunction + disableScrollBars + scrollToPageFunction + getScrollPosition;
		html = InjectJavascript(html, js);
		foreach (var image in book.Images)
		{
			html = ReplaceImageUrls(html, image.FileName, image.ImageUrl);
		}
		return html;
	}

	[GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
	private static partial Regex StyleTagRegex();

	[GeneratedRegex("font-size:\\s*\\d+px\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
	private static partial Regex FontSizeRegex();

	[GeneratedRegex("font-family:\\s*[^;]+?\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
	private static partial Regex FontFamilyRegex();

	static string RemoveStyleTags(string html)
	{
		return StyleTagRegex().Replace(html, string.Empty);
	}

	static string InjectCss(string html, string css)
	{
		// Assuming you want to inject the Css into the <head> section
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
        // Define a timeout for the regex operations
        TimeSpan regexTimeout = TimeSpan.FromSeconds(2);

        // Handle standard img tags
        string imgPattern = $@"<img[^>]*src=[""']([^""']*{sourcePattern}[^""']*)[""'][^>]*>";
        htmlContent = Regex.Replace(htmlContent, imgPattern, match =>
        {
            string originalTag = match.Value;
            return originalTag.Replace(match.Groups[1].Value, newImageSource);
        }, RegexOptions.None, regexTimeout);

        // Handle SVG image tags
        string svgPattern = $@"<image[^>]*xlink:href=[""']([^""']*{sourcePattern}[^""']*)[""'][^>]*>";
        htmlContent = Regex.Replace(htmlContent, svgPattern, match =>
        {
            string originalTag = match.Value;
            return originalTag.Replace(match.Groups[1].Value, newImageSource);
        }, RegexOptions.None, regexTimeout);

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