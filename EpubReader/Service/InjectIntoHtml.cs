using System.Text.RegularExpressions;
using EpubReader.Models;

namespace EpubReader.Service;

public static partial class InjectIntoHtml
{
	public static string InjectAllCss(string html, Book book, Settings settings)
	{
		if (string.IsNullOrEmpty(html))
		{
			return string.Empty;
		}
		// Remove existing <style> tags
		html = RemoveStyleTags(html);

		var otherCss = book.Css[^1].Content ?? string.Empty;
		otherCss += book.Css[0].Content ?? string.Empty;
		otherCss += disableTouchCSS;
		string styleTag = GenerateCSSFromString(html, settings);

		otherCss = FilterCss(otherCss, settings);
		styleTag += otherCss;


		// Inject the combined Css into the HTML
		if (!string.IsNullOrEmpty(styleTag))
		{
			html = InjectCss(html, styleTag);
		}
		html = FixSelfClosingAnchorInH1(html);
		foreach (var image in book.Images)
		{
			html = ReplaceImageUrls(html, image.FileName, image.ImageUrl);
		}
		html = InjectJavascript(html, paginationJS);
		return WrapBodyContent(html, "ebook-content");
	}

	static string RemoveStyleTags(string html)
	{
		return StyleTagRegex().Replace(html, string.Empty);
	}

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

	static string WrapBodyContent(string htmlString, string wrapperId)
	{
		var bodyStartMatch = BodyStartReg().Match(htmlString);
		var bodyEndMatch = BodyEndRegex().Match(htmlString);

		if (bodyStartMatch.Success && bodyEndMatch.Success)
		{
			int bodyStartIndex = bodyStartMatch.Index + bodyStartMatch.Length;
			int bodyEndIndex = bodyEndMatch.Index;
			string bodyContent = htmlString[bodyStartIndex..bodyEndIndex];

			string sectionBlockPattern = @"<section[^>]*>(?<sectionContent>.*?)</section>";
			Regex sectionRegex = new(sectionBlockPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromMilliseconds(5000));

			Match sectionMatch = sectionRegex.Match(bodyContent);

			string modifiedBodyContent = bodyContent; // Start with original body content

			if (sectionMatch.Success)
			{
				string sectionContent = sectionMatch.Groups["sectionContent"].Value;
				modifiedBodyContent = bodyContent.Replace(sectionMatch.Value, sectionContent);
			}

			string wrappedBody = $"<div id=\"{wrapperId}\">{modifiedBodyContent}</div>";
			return string.Concat(htmlString.AsSpan(0, bodyStartIndex), wrappedBody, htmlString.AsSpan(bodyEndIndex));
		}
		else
		{
			// Fallback if <body> tags are not found
			return $"<div id=\"{wrapperId}\">{htmlString}</div>";
		}
	}

	static string FixSelfClosingAnchorInH1(string htmlString)
	{
		// Regex to find self-closing anchor tags *inside* h1 tags with class="ch"
		// and capture the text immediately following it.
		string pattern = @"<h1 class=""ch""><a(?=[^>]*\/>)([^>]*)/>(?<followingText>.*?)<\/h1>";
		Regex regex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromMilliseconds(5000));

		string modifiedHtml = regex.Replace(htmlString, match =>
		{
			if (match.Success)
			{
				string anchorAttributes = match.Groups[1].Value; // Capture attributes within <a> tag
				string followingText = match.Groups["followingText"].Value; // Capture text after self-closing tag

				// Reconstruct with properly opened and closed anchor tag wrapping the following text
				return $"<h1 class=\"ch\"><a{anchorAttributes}>{followingText}</a></h1>";
			}
			return match.Value; // If no match, return the original match (no replacement)
		});

		return modifiedHtml;
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
        TimeSpan regexTimeout = TimeSpan.FromSeconds(20);

		// Handle standard img tags
		string imgPattern = $@"<img[^>]*src=[""']([^""']*{sourcePattern}[^""']*)[""'][^>]*>";
		
		htmlContent = Regex.Replace(htmlContent, imgPattern, match =>
        {
            string originalTag = match.Value;
            return originalTag.Replace(match.Groups[1].Value, newImageSource);
        }, RegexOptions.None, regexTimeout);

		// Handle img tags with additional attributes
		string imgPattern2 = @"<img[^>]*src=[""']([^""']*)[""'][^>]*>";

		// Replace the src attribute value with the new image source
		htmlContent = Regex.Replace(htmlContent, imgPattern2, match =>
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

	[GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 20000)]
	private static partial Regex StyleTagRegex();

	[GeneratedRegex("font-size:\\s*\\d+px\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontSizeRegex();

	[GeneratedRegex("font-family:\\s*[^;]+?\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontFamilyRegex();

	[GeneratedRegex(@"<body[^>]*>", RegexOptions.IgnoreCase, "en-CA")]
	private static partial Regex BodyStartReg();

	[GeneratedRegex(@"</body>", RegexOptions.IgnoreCase, "en-CA")]
	private static partial Regex BodyEndRegex();

	static readonly string paginationJS = @"
let pages = [];
let currentPageIndex = 0;
let contentDivId = 'ebook-content'; // ID of the div containing your ebook chapter

function paginateContent(contentElementId) {
    const contentElement = document.getElementById(contentElementId);
    if (!contentElement) {
        console.error(""Content element not found:"", contentElementId);
        return [];
    }

    const pageHeight = window.innerHeight; // Or use a more specific container height if needed
    const pageWidth = window.innerWidth; // Or container width

    const originalContent = contentElement.innerHTML;
    const tempContainer = document.createElement('div');
    tempContainer.style.visibility = 'hidden'; // Keep it hidden while measuring
    tempContainer.style.position = 'absolute'; // Avoid layout interference
    document.body.appendChild(tempContainer);

    let pages = [];
    let currentPageHTML = '';
    tempContainer.innerHTML = ''; // Clear temporary container

    // Array of child nodes to iterate, allows text nodes and element nodes
    const childNodes = Array.from(contentElement.childNodes);

    for (const node of childNodes) {
        tempContainer.appendChild(node.cloneNode(true)); // Append a clone to measure

        if (tempContainer.offsetHeight > pageHeight) {
            // Current page is full
            pages.push(currentPageHTML);

            // Start a new page, but importantly, re-add the node that overflowed
            currentPageHTML = tempContainer.innerHTML; // Start new page with overflowing content
            tempContainer.innerHTML = ''; // Clear for next page measurement
            tempContainer.appendChild(node.cloneNode(true)); // Re-append to start next page measure
        } else {
            currentPageHTML = tempContainer.innerHTML; // Update current page content
        }
    }

    // Add the last page if there's content left
    if (currentPageHTML) {
        pages.push(currentPageHTML);
    }

    document.body.removeChild(tempContainer); // Clean up temporary container
    return pages;
}

function initializePagination() {
    pages = paginateContent(contentDivId);
    if (pages.length > 0) {
        displayPage(0); // Display the first page
    } else {
        document.getElementById(contentDivId).innerHTML = ""Error paginating content."";
    }
    updatePageIndicator(); // Call to update page number display in UI
}

function displayPage(pageIndex) {
    if (pageIndex >= 0 && pageIndex < pages.length) {
        document.getElementById(contentDivId).innerHTML = pages[pageIndex];
        currentPageIndex = pageIndex;
        updatePageIndicator();
    } else {
        console.warn(""Page index out of bounds:"", pageIndex);
    }
}

function nextPage() {
    if (currentPageIndex < pages.length - 1) {
        displayPage(currentPageIndex + 1);
    }
}

function previousPage() {
    if (currentPageIndex > 0) {
        displayPage(currentPageIndex - 1);
    }
}

function updatePageIndicator() {
    const pageIndicator = document.getElementById('page-indicator'); // Assuming you have a span for this
    if (pageIndicator) {
        pageIndicator.textContent = `Page ${currentPageIndex + 1} of ${pages.length}`;
    }
}

function isFirstPage() {
    return currentPageIndex === 0 ? 'Yes' : 'No';
}

function isLastPage() {
    return currentPageIndex === pages.length - 1 ? 'Yes' : 'No';
}

// Call initializePagination when the HTML is loaded
document.addEventListener('DOMContentLoaded', initializePagination);
";

	static readonly string disableTouchCSS = @"
		* {
				-webkit-touch-callout: none;
				-webkit-user-select: none;
				-khtml-user-select: none;
				-moz-user-select: none;
				-ms-user-select: none;
				user-select: none;
			}";
}