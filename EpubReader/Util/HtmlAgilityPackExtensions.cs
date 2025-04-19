﻿using System.Text;
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

	public static string EnsureDoctypeDeclaration(string html)
	{
		if (string.IsNullOrEmpty(html))
		{
			return "<!DOCTYPE html>\n<html><head><title>Initial Document</title></head><body></body></html>"; // Return a minimal valid HTML
		}

		// Use a regular expression to check for the DOCTYPE declaration (case-insensitive).
		Regex doctypeRegex = DocType();

		if (!doctypeRegex.IsMatch(html))
		{
			// If the DOCTYPE declaration is not found, add it to the beginning of the string.
			//  It's crucial to add a newline after the doctype for better formatting and to avoid issues.
			return "<!DOCTYPE html>\n" + html;
		}

		// If the DOCTYPE declaration is already present, return the original HTML string.
		return html;
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
	public static string CheckAndRemoveKoboAndCalibreCss(string cssContent)
	{
		if (cssContent.Contains(".kobo") || cssContent.Contains(".calibre"))
		{
			return string.Empty;
		}
		return cssContent;
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

	public static string AddJsLinks(string htmlContent, List<string> jsFiles)
	{
		// Find the closing </head> tag
		int headCloseTagIndex = htmlContent.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);

		if (headCloseTagIndex == -1)
		{
			throw new InvalidOperationException("The HTML content does not contain a closing </head> tag.");
		}
		if(jsFiles.Contains("kobo"))
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
		string pattern = @"<style[^>]*id\s*=\s*[""']kobostylehacks[""'][^>]*>.*?</style>";
		string pattern1 = @"<style[^>]*id=""kobostylehacks""[^>]*>.*?</style>";
		html = Regex.Replace(html, pattern1, string.Empty, RegexOptions.Singleline, matchTimeout: TimeSpan.FromSeconds(10));
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

		string pattern3 = @"\.calibre\d+\s*{[^{}]*}";
		result = Regex.Replace(result, pattern3, string.Empty, RegexOptions.None, TimeSpan.FromSeconds(10));

		// Clean up any consecutive newlines
		result = CleanNewLines().Replace(result, Environment.NewLine);
		return result;
	}

	[GeneratedRegex(@"url\(['""]?(.*?)['""]?\)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex CssContentFilter();
	[GeneratedRegex(@"(\r?\n){2,}", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex CleanNewLines();

	[GeneratedRegex(@"<!DOCTYPE\s+html\s*>", RegexOptions.IgnoreCase,matchTimeoutMilliseconds: 2000, "en-US")]
	private static partial Regex DocType();
}