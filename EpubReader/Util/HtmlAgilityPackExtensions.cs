using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace EpubReader.Util;

public static class HtmlAgilityPackExtensions
{
	public static List<string> GetCssFiles(this HtmlDocument doc)
	{
		List<string> cssFiles = [];

		// XPath to find all <link> tags with rel="stylesheet"
		var linkNodes = doc.DocumentNode.SelectNodes("//link[@rel='stylesheet']");

		if (linkNodes != null)
		{
			foreach (var linkNode in linkNodes)
			{
				// Get the value of the href attribute
				var hrefAttribute = linkNode.Attributes["href"];
				if (hrefAttribute != null)
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
		string cleanedHtml = Regex.Replace(htmlContent, cssLinkPattern, string.Empty, RegexOptions.IgnoreCase);
		return RemoveEmptyLines(cleanedHtml);
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
			cssLinks.Append($"<link rel=\"stylesheet\" href=\"{cssFile}\"/>\n");
		}

		// Insert the CSS links before the closing </head> tag
		string updatedHtmlContent = htmlContent.Insert(headCloseTagIndex, cssLinks.ToString());
		return RemoveEmptyLines(updatedHtmlContent);
	}
	static string RemoveEmptyLines(string input)
	{
		var lines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
		return string.Join(Environment.NewLine, lines);
	}
}