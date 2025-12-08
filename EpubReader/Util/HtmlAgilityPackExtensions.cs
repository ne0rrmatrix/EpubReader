using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace EpubReader.Util;

/// <summary>
/// Provides extension methods for processing HTML content using Html Agility Pack.
/// </summary>
/// <remarks>This static class includes methods for manipulating and extracting information from HTML documents,
/// such as extracting CSS file names, ensuring XHTML compliance, and modifying HTML content by adding or removing CSS
/// and JavaScript links. It is designed to work with HTML content represented as strings.</remarks>
public static partial class HtmlAgilityPackExtensions
{
	/// <summary>
	/// Extracts the names of CSS files linked in an HTML document.
	/// </summary>
	/// <remarks>This method parses the provided HTML content to find all <c>&lt;link&gt;</c> elements with a
	/// <c>rel</c> attribute of "stylesheet" and extracts the file names from their <c>href</c> attributes.</remarks>
	/// <param name="htmlFile">The HTML content as a string from which to extract CSS file names.</param>
	/// <returns>A list of strings containing the names of CSS files linked in the HTML document. The list will be empty if no CSS
	/// files are found.</returns>
	public static List<string> GetCssFiles(string htmlFile)
	{
		var doc = new HtmlDocument();
		doc.LoadHtml(htmlFile);
		List<string> cssFiles = [];

		// XPath to find all <link> tags with rel="stylesheet"
		var linkNodes = doc.DocumentNode.SelectNodes("//link[@rel='stylesheet']");

		if (linkNodes is not null)
		{
			foreach (var linkNode in linkNodes)
			{
				// Get the value of the href attribute
				var hrefAttribute = linkNode.Attributes["href"];
				if (hrefAttribute is not null)
				{
					var fileName = Path.GetFileName(hrefAttribute.Value);
					cssFiles.Add(fileName);
				}
			}
		}

		return cssFiles;
	}

	/// <summary>
	/// Ensures that the provided XHTML string contains an <c>xml:lang</c> attribute in the root element.
	/// </summary>
	/// <remarks>If the input is not a valid XHTML document or if parsing fails, the method returns the original
	/// input.</remarks>
	/// <param name="xhtml">The XHTML content to check and potentially modify.</param>
	/// <returns>The original XHTML string if it already contains an <c>xml:lang</c> attribute in the root element; otherwise, a
	/// modified XHTML string with the <c>xml:lang</c> attribute set to <see langword="en-US"/>.</returns>
	public static string EnsureXmlLang(string xhtml)
	{
		try
		{
			if (IsHtmlPage(xhtml))
			{
				return xhtml;
			}
			var doc = XDocument.Parse(xhtml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

			// Get the root element (usually <html>)
			var root = doc.Root;
			if (root is null)
			{
				return xhtml;
			}

			// Check for xml:lang attribute (must use the XML namespace)
			XNamespace xmlNs = XNamespace.Xml;
			var xmlLangAttr = root.Attribute(xmlNs + "lang");

			if (xmlLangAttr is null)
			{
				root.SetAttributeValue(xmlNs + "lang", "en-US");
				return doc.ToString(SaveOptions.DisableFormatting);
			}

			// Already set, return original
			return xhtml;
		}
		catch
		{
			// If parsing fails, return original input
			return xhtml;
		}
	}

	/// <summary>
	/// Removes all CSS link tags from the specified HTML content.
	/// </summary>
	/// <remarks>This method uses a regular expression to identify and remove <c>&lt;link&gt;</c> tags with
	/// <c>rel="stylesheet"</c> from the provided HTML content. It also removes any resulting empty lines.</remarks>
	/// <param name="htmlContent">The HTML content from which CSS link tags should be removed.</param>
	/// <returns>The HTML content with all CSS link tags removed.</returns>
	public static string RemoveCssLinks(string htmlContent)
	{
		// Regular expression to match <link> tags with rel="stylesheet"
		string cssLinkPattern = @"<link\s+[^>]*rel=[""']stylesheet[""'][^>]*>";
		// Remove all matches from the HTML content
		string cleanedHtml = Regex.Replace(htmlContent, cssLinkPattern, string.Empty, RegexOptions.IgnoreCase, matchTimeout: TimeSpan.FromSeconds(10));
		return RemoveEmptyLines(cleanedHtml);
	}

	/// <summary>
	/// Inserts a CSS link tag into the HTML content before the closing <c>&lt;/head&gt;</c> tag.
	/// </summary>
	/// <param name="htmlContent">The HTML content into which the CSS link will be inserted. Must contain a closing <c>&lt;/head&gt;</c> tag.</param>
	/// <param name="cssFile">The path or URL of the CSS file to be linked in the HTML content.</param>
	/// <returns>The updated HTML content with the CSS link tag inserted.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the <paramref name="htmlContent"/> does not contain a closing <c>&lt;/head&gt;</c> tag.</exception>
	public static string AddCssLink(string htmlContent, string cssFile)
	{
		// Find the closing </head> tag
		int headCloseTagIndex = htmlContent.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);

		if (headCloseTagIndex == -1)
		{
			throw new InvalidOperationException("The HTML content does not contain a closing </head> tag.");
		}

		// Create <link> tags for each CSS file
		StringBuilder cssLinks = new();
		cssLinks.Append($"<link rel=\"stylesheet\" href=\"{cssFile}\"/>\n");

		// Insert the CSS links before the closing </head> tag
		string updatedHtmlContent = htmlContent.Insert(headCloseTagIndex, cssLinks.ToString());
		return RemoveEmptyLines(updatedHtmlContent);
	}

	/// <summary>
	/// Inserts CSS link elements into the HTML content before the closing <c>&lt;/head&gt;</c> tag.
	/// </summary>
	/// <param name="htmlContent">The HTML content into which the CSS links will be inserted. Must contain a closing <c>&lt;/head&gt;</c> tag.</param>
	/// <param name="cssFiles">A list of CSS file paths to be included as link elements. Files containing "kobo" or "calibre" in their names are
	/// ignored.</param>
	/// <returns>The updated HTML content with the CSS links inserted before the closing <c>&lt;/head&gt;</c> tag.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the <paramref name="htmlContent"/> does not contain a closing <c>&lt;/head&gt;</c> tag.</exception>
	public static string AddCssLinks(string htmlContent, List<string> cssFiles)
	{
		// Find the closing </head> tag
		int headCloseTagIndex = htmlContent.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);

		if (headCloseTagIndex == -1)
		{
			throw new InvalidOperationException("The HTML content does not contain a closing </head> tag.");
		}

		// Create <link> tags for each CSS file
		StringBuilder cssLinks = new();

		foreach (string cssFile in cssFiles)
		{
			if (cssFile.Contains("kobo") || cssFile.Contains("calibre"))
			{
				continue;
			}
			cssLinks.Append($"<link rel=\"stylesheet\" href=\"{cssFile}\"/>\n");
		}

		// Insert the CSS links before the closing </head> tag
		string updatedHtmlContent = htmlContent.Insert(headCloseTagIndex, cssLinks.ToString());
		return RemoveEmptyLines(updatedHtmlContent);
	}

	/// <summary>
	/// Inserts JavaScript file links into the HTML content before the closing <c>&lt;/head&gt;</c> tag.
	/// </summary>
	/// <param name="htmlContent">The HTML content into which the JavaScript links will be inserted. Must contain a closing <c>&lt;/head&gt;</c> tag.</param>
	/// <param name="jsFiles">A list of JavaScript file paths to be included as <c>&lt;script&gt;</c> tags. If the list contains "kobo" or
	/// "calibre", no changes will be made to the HTML content.</param>
	/// <returns>The updated HTML content with the JavaScript links inserted before the closing <c>&lt;/head&gt;</c> tag.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the <paramref name="htmlContent"/> does not contain a closing <c>&lt;/head&gt;</c> tag.</exception>
	public static string AddJsLinks(string htmlContent, List<string> jsFiles)
	{
		// Find the closing </head> tag
		int headCloseTagIndex = htmlContent.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);

		if (headCloseTagIndex == -1)
		{
			throw new InvalidOperationException("The HTML content does not contain a closing </head> tag.");
		}
		if (jsFiles.Contains("kobo"))
		{
			return htmlContent;
		}
		if (jsFiles.Contains("calibre"))
		{
			return htmlContent;
		}
		// Create <link> tags for each CSS file
		StringBuilder jsLinks = new();
		foreach (string jsFile in jsFiles)
		{
			jsLinks.Append($"<script src=\"{jsFile}\"></script>\n");
		}

		// Insert the CSS links before the closing </head> tag
		string updatedHtmlContent = htmlContent.Insert(headCloseTagIndex, jsLinks.ToString());
		return RemoveEmptyLines(updatedHtmlContent);
	}

	/// <summary>
	/// Ensures that the specified HTML content includes an XHTML 1.0 Transitional doctype declaration.
	/// </summary>
	/// <remarks>This method checks if the provided HTML content contains any doctype declaration. If no doctype is
	/// found, it prepends the XHTML 1.0 Transitional doctype to the content.</remarks>
	/// <param name="htmlContent">The HTML content to check and potentially modify. If the content is <see langword="null"/> or empty, it is returned
	/// unchanged.</param>
	/// <returns>The original HTML content if it already contains a doctype declaration; otherwise, the content prefixed with an
	/// XHTML 1.0 Transitional doctype.</returns>
	public static string EnsureXhtml1TransitionalDoctype(string htmlContent)
	{
		const string xhtmlDoctype = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">";
		const string doctypeKeyword = "<!DOCTYPE"; // The keyword to check for any doctype

		if (string.IsNullOrEmpty(htmlContent) || htmlContent.Contains(doctypeKeyword, StringComparison.OrdinalIgnoreCase))
		{
			return htmlContent; // If content is empty, just return it
		}

		// If no <!DOCTYPE was found, then proceed to add the XHTML 1.0 Transitional doctype
		return xhtmlDoctype + Environment.NewLine + htmlContent;
	}

	/// <summary>
	/// Updates the image URL in the provided HTML string by replacing the URL with the file name.
	/// </summary>
	/// <remarks>This method parses the input HTML to locate an image element and modifies its xlink:href attribute 
	/// to contain only the file name extracted from the original URL. If the image element or the URL is  not found, or if
	/// an exception occurs, the method returns the original HTML string.</remarks>
	/// <param name="html">The HTML string containing an image element with an xlink:href attribute.</param>
	/// <returns>A string representing the updated HTML with the image URL replaced by the file name.  If an error occurs during
	/// processing, the original HTML string is returned.</returns>
	public static string UpdateImageUrl(string html)
	{
		try
		{
			XDocument doc = XDocument.Parse(html);
			XNamespace xlink = "http://www.w3.org/1999/xlink";

			var imageElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "image");
			if (imageElement is not null)
			{
				string? originalUrl = imageElement.Attribute(xlink + "href")?.Value;
				if (originalUrl is not null)
				{
					string fileName = Path.GetFileName(originalUrl);
					imageElement.SetAttributeValue(xlink + "href", fileName);
				}
			}

			return doc.ToString();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Trace.TraceInformation(ex.Message);
			return html;
		}

	}

	/// <summary>
	/// Updates the image paths in the provided CSS content to use only the file names.
	/// </summary>
	/// <param name="cssContent">The CSS content containing image paths to be updated.</param>
	/// <returns>A string with the updated CSS content where image paths are replaced by their file names.</returns>
	public static string UpdateImagePathsForCSSFiles(string cssContent)
	{
		return CssContentFilter().Replace(cssContent, match =>
		{
			string url = match.Groups[1].Value;
			string fileName = Path.GetFileName(url);
			return $"url('{fileName}')";
		});
	}

	/// <summary>
	/// Removes all CSS rules that contain "calibre" from the input CSS string.
	/// This includes class selectors like .calibre, .calibre1, etc.,
	/// and pseudo-class selectors like .pcalibre:link.
	/// </summary>
	/// <param name="cssString">The input CSS string.</param>
	/// <returns>The CSS string with calibre references removed.</returns>
	public static string RemoveCalibreReferences(string cssString)
	{
		// This regex looks for CSS rules starting with ".calibre" (and any digits after it)
		// or ".pcalibre" (and any digits after it) followed by any pseudo-class,
		// and captures the entire rule block, including the curly braces and their content.
		// It's designed to be non-greedy (.*?) to match only up to the next closing brace.
		string pattern = @"\.(p?calibre\d*)\s*\{[^}]*\}";

		// Use Regex.Replace to remove all matches of the pattern.
		return Regex.Replace(cssString, pattern, "", RegexOptions.Singleline, TimeSpan.FromSeconds(10));
	}

	/// <summary>
	/// Removes Kobo JavaScript script tags from the specified HTML content.
	/// </summary>
	/// <remarks>This method uses a regular expression to identify and remove <c>&lt;script&gt;</c> tags with
	/// <c>src</c> attributes containing "kobo.js" from the provided HTML content. It also removes any resulting empty lines.</remarks>
	/// <param name="htmlContent">The HTML content from which Kobo script tags should be removed.</param>
	/// <returns>The HTML content with all Kobo script tags removed.</returns>
	public static string RemoveKoboScriptLinks(string htmlContent)
	{
		// Regular expression to match <script> tags with src containing "kobo.js"
		string koboScriptPattern = @"<script\s+[^>]*src=[""'][^""']*kobo\.js[""'][^>]*/?>|<script\s+[^>]*src=[""'][^""']*kobo\.js[""'][^>]*></script>";
		// Remove all matches from the HTML content
		string cleanedHtml = Regex.Replace(htmlContent, koboScriptPattern, string.Empty, RegexOptions.IgnoreCase, matchTimeout: TimeSpan.FromSeconds(10));
		return RemoveEmptyLines(cleanedHtml);
	}

	static bool IsHtmlPage(string htmlContent)
	{
		try
		{
			HtmlDocument doc = new();
			doc.LoadHtml(htmlContent);

			// If there are parsing errors, it might not be well-formed HTML,
			// but Html Agility Pack is very forgiving.
			// A better check is to see if the root HTML element exists.
			return doc.DocumentNode.Descendants("html").Any();
		}
		catch
		{
			return false; // Not valid HTML according to the parser
		}
	}

	static string RemoveEmptyLines(string input)
	{
		var lines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
		return string.Join(Environment.NewLine, lines);
	}

	[GeneratedRegex(@"url\(['""]?(.*?)['""]?\)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex CssContentFilter();
}