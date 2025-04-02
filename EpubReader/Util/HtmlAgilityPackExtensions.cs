using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace EpubReader.Util;

public static partial class HtmlAgilityPackExtensions
{
	public static List<string> GetCssFiles(this HtmlDocument doc)
	{
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

	public static string RemoveCssLinks(string htmlContent)
	{
		// Regular expression to match <link> tags with rel="stylesheet"
		string cssLinkPattern = @"<link\s+[^>]*rel=[""']stylesheet[""'][^>]*>";
		// Remove all matches from the HTML content
		string cleanedHtml = Regex.Replace(htmlContent, cssLinkPattern, string.Empty, RegexOptions.IgnoreCase, matchTimeout: TimeSpan.FromSeconds(10));
		return RemoveEmptyLines(cleanedHtml);
	}

	public static string AddCssLink(string htmlContent, string cssFile)
	{
		// Skip calibre-specific CSS files
		if (cssFile.Contains(".calibre"))
		{
			return htmlContent;
		}
		// Skip Kobo-specific CSS files
		if (cssFile.StartsWith("kobo"))
		{
			return htmlContent;
		}
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
			// Skip calibre-specific CSS files
			if (cssFile.Contains(".calibre"))
			{
				continue;
			}
			// Skip Kobo-specific CSS files
			if (cssFile.StartsWith("kobo"))
			{
				continue;
			}
			cssLinks.Append($"<link rel=\"stylesheet\" href=\"{cssFile}\"/>\n");
		}

		// Insert the CSS links before the closing </head> tag
		string updatedHtmlContent = htmlContent.Insert(headCloseTagIndex, cssLinks.ToString());
		return RemoveEmptyLines(updatedHtmlContent);
	}

	public static string AddJsLinks(string htmlContent, List<string> jsFiles)
	{
		// Find the closing </head> tag
		int headCloseTagIndex = htmlContent.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);

		if (headCloseTagIndex == -1)
		{
			throw new InvalidOperationException("The HTML content does not contain a closing </head> tag.");
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

	static string RemoveEmptyLines(string input)
	{
		var lines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
		return string.Join(Environment.NewLine, lines);
	}

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
	public static string RemoveKoboHacks(string html)
	{
		string pattern = @"<style[^>]*id=""kobostylehacks""[^>]*>.*?</style>";
		return Regex.Replace(html, pattern, string.Empty, RegexOptions.Singleline, matchTimeout: TimeSpan.FromSeconds(10));
	}

	public static string UpdateImagePathsForCSSFiles(string cssContent)
	{
		return CssContentFilter().Replace(cssContent, match =>
		{
			string url = match.Groups[1].Value;
			string fileName = Path.GetFileName(url);
			return $"url('{fileName}')";
		});
	}

	public static string RemoveCalibreAndKoboRules(string cssText)
	{
		// First remove all .calibre rules
		string pattern1 = @"\.calibre\w*\s*{[^{}]*}";
		string intermediate = Regex.Replace(cssText, pattern1, string.Empty, RegexOptions.None, TimeSpan.FromSeconds(10));

		// Then remove all .kobo rules
		string pattern2 = @"\.kobo\w*\s*{[^{}]*}";
		string result = Regex.Replace(intermediate, pattern2, string.Empty, RegexOptions.None, TimeSpan.FromSeconds(10));

		// Clean up any consecutive newlines
		result = CleanNewLines().Replace(result, Environment.NewLine);

		return result;
	}

	[GeneratedRegex(@"url\(['""]?(.*?)['""]?\)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex CssContentFilter();
	[GeneratedRegex(@"(\r?\n){2,}", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex CleanNewLines();
}