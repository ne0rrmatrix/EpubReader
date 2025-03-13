using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace EpubReader.Util;

public static partial class ImageExtensions
{
	[GeneratedRegex(@"\s*height\s*=\s*""100%""", RegexOptions.Compiled, matchTimeoutMilliseconds: 20000)]
	private static partial Regex CleanedTag();

	static readonly TimeSpan regexTimeout = TimeSpan.FromSeconds(20);
	static string jpg => "image/jpeg";
	static string png => "image/png";
	static string gif => "image/gif";
	static string webp => "image/webp";
	static string jpeg => "image/jpeg";

	/// <summary>
	/// Replaces image URLs in HTML with base64 encoded images.
	/// </summary>
	/// <param name="inputString"></param>
	/// <returns></returns>
	public static string FixImageTags(string inputString)
	{
		// Regex to find <img> tags with height="100%"
		string pattern = @"<img(?=[^>]*height\s*=\s*""100%""[^>]*)([^>]*)>";

		// Replace the matched <img> tags, removing the height attribute.
		string result = Regex.Replace(inputString, pattern, match =>
		{
			string imgTag = match.Value;

			// Remove height="100%" using another regex.
			string cleanedImgTag = CleanedTag().Replace(imgTag, "");

			return cleanedImgTag;
		}, RegexOptions.Compiled, regexTimeout);

		if (string.IsNullOrEmpty(result))
		{
			return inputString;
		}
		return result;
	}

	/// <summary>
	/// Replaces image URLs in HTML with local image URLs.
	/// </summary>
	/// <param name="inputString"></param>
	/// <param name="images"></param>
	/// <returns></returns>
	public static string ReplaceImageUrls(string? inputString, List<Models.Image> images)
	{
		if (string.IsNullOrEmpty(inputString))
		{
			return string.Empty;
		}
		var temp = ExtractImageUrls(inputString);
		var svgImages = ExtractImageFilenamesFromSvg(inputString);
		temp.AddRange(svgImages);
		foreach (var item in temp)
		{
			var fileName = Path.GetFileName(item);
			var image = images.FirstOrDefault(x => x.FileName == fileName);
			inputString = ReplaceImageUrl(inputString, fileName, image?.ImageUrl ?? "");
		}

		return inputString;
	}

	/// <summary>
	/// Replaces image URLs in the input string with the corresponding base64-encoded image data.
	/// </summary>
	/// <param name="inputString"></param>
	/// <param name="images"></param>
	/// <returns></returns>
	public static string ReplaceCssUrls(string? inputString, List<Models.Image> images)
	{
		if (string.IsNullOrEmpty(inputString))
		{
			return string.Empty;
		}
		foreach (var image in images)
		{
			inputString = ReplaceCssImageUrl(inputString, image.FileName, image.ImageUrl);
		}
		return inputString;
	}

	/// <summary>
	/// Replaces image URLs in the input string with base64 encoded images.
	/// </summary>
	/// <param name="inputString"></param>
	/// <param name="imageName"></param>
	/// <param name="imageString"></param>
	/// <returns></returns>
	public static string ReplaceImageUrl(string inputString, string imageName, string imageString)
	{
		var base64String = $"data:{GetMimeType(imageName)};base64,{imageString}";

		// For HTML encoded version
		var htmlEncodedString = HtmlEncoder.Default.Encode(imageString);
		var htmlEncodedBase64String = $"data:{GetMimeType(imageName)};base64,{htmlEncodedString}";

		string escapedFullImageName = Regex.Escape(imageName);

		// HTML img tag pattern
		string imgPattern = $@"<img[^>]*src=[""']([^""']*{escapedFullImageName}[^""']*)[""'][^>]*>";
		string imgPattern2 = @"<img[^>]*src=[""']([^""']*)[""'][^>]*>";

		// SVG image pattern - improved to better match XML
		string svgPattern = $@"<image[^>]*xlink:href=[""']([^""']*{escapedFullImageName})[""'][^>]*>";

		// Generic SVG pattern to catch other cases
		string svgGenericPattern = @"<image[^>]*xlink:href=[""']([^""']*)[""'][^>]*>";

		// Replace HTML img tags
		inputString = Regex.Replace(inputString, imgPattern, match =>
		{
			string originalTag = match.Value;
			string fileName = ExtractFilenameFromImgTag(originalTag);

			if (!IsMatchingFilename(fileName, imageName))
			{
				return originalTag;
			}

			return originalTag.Replace(match.Groups[1].Value, htmlEncodedBase64String);
		}, RegexOptions.Compiled, regexTimeout);

		inputString = Regex.Replace(inputString, imgPattern2, match =>
		{
			string originalTag = match.Value;
			string fileName = ExtractFilenameFromImgTag(originalTag);

			if (!IsMatchingFilename(fileName, imageName))
			{
				return originalTag;
			}

			return originalTag.Replace(match.Groups[1].Value, htmlEncodedBase64String);
		}, RegexOptions.Compiled, regexTimeout);

		// Replace SVG image references with specific pattern
		inputString = Regex.Replace(inputString, svgPattern, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, base64String);
		}, RegexOptions.IgnoreCase, regexTimeout);

		// Handle SVG with generic pattern
		inputString = Regex.Replace(inputString, svgGenericPattern, match =>
		{
			string originalTag = match.Value;
			string fileName = Path.GetFileName(match.Groups[1].Value.TrimEnd('/'));

			if (!IsMatchingFilename(fileName, imageName))
			{
				return originalTag;
			}

			return originalTag.Replace(match.Groups[1].Value, base64String);
		}, RegexOptions.IgnoreCase, regexTimeout);

		return inputString;
	}

	static List<string> ExtractImageUrls(string html)
	{
		var htmlDoc = new HtmlDocument();
		htmlDoc.LoadHtml(html);

		// Find all img tags
		var imgNodes = htmlDoc.DocumentNode.SelectNodes("//img");

		// If no images found, return empty list
		if (imgNodes == null)
		{
			return [];
		}

		// Extract the src attribute from each image
		List<string> imageUrls = [.. imgNodes
			.Select(node => node.GetAttributeValue("src", string.Empty))
			.Where(src => !string.IsNullOrEmpty(src))];

		return imageUrls;
	}

	static string ReplaceCssImageUrl(string inputString, string imageName, string imageString)
	{
		var base64String = $"data:{GetMimeType(imageName)};base64,{imageString}";
		string escapedImageName = Regex.Escape(Path.GetFileNameWithoutExtension(imageName));

		// CSS pattern
		string patternCss = $@"background:\s*url\(\s*['""]?([^'""]*/)*{escapedImageName}(\.[a-zA-Z]+)?['""]?\s*\)\s*(no-repeat\s*50%\s*)?;";
		string replacement = $"background-image: url({base64String});\nbackground-repeat: no-repeat;\nbackground-position: 50%;";

		// Replace CSS backgrounds
		inputString = Regex.Replace(inputString, patternCss, replacement, RegexOptions.IgnoreCase, regexTimeout);
		return inputString;
	}

	static bool IsMatchingFilename(string filename1, string filename2)
	{
		if (string.IsNullOrEmpty(filename1) || string.IsNullOrEmpty(filename2))
		{
			return false;
		}

		return string.Equals(
			Path.GetFileName(filename1.TrimEnd('/')),
			Path.GetFileName(filename2.TrimEnd('/')),
			StringComparison.OrdinalIgnoreCase
		);
	}

	static string ExtractFilenameFromImgTag(string imgTagString)
	{
		try
		{
			// Find the src attribute
			int srcIndex = imgTagString.IndexOf("src=\"");
			if (srcIndex == -1)
			{
				return string.Empty;
			}

			// Move to the start of the path
			srcIndex += 5; // Length of 'src="'

			// Find the closing quote
			int endIndex = imgTagString.IndexOf('"', srcIndex);
			if (endIndex == -1)
			{
				return string.Empty;
			}

			// Extract the full path
			string fullPath = imgTagString[srcIndex..endIndex];

			// Get the filename (everything after the last slash or backslash)
			int lastSlashIndex = Math.Max(fullPath.LastIndexOf('/'), fullPath.LastIndexOf('\\'));

			if (lastSlashIndex == -1)
			{
				return fullPath; // No slash found, the path is just the filename
			}

			return fullPath[(lastSlashIndex + 1)..];
		}
		catch
		{
			return string.Empty;
		}
	}

	static List<string> ExtractImageFilenamesFromSvg(string htmlContent)
	{
		var imageFilenames = new List<string>();
		var htmlDoc = new HtmlDocument();

		// Load the HTML content
		htmlDoc.LoadHtml(htmlContent);

		// Find all SVG nodes
		var svgNodes = htmlDoc.DocumentNode.SelectNodes("//svg");

		if (svgNodes != null)
		{
			foreach (var svgNode in svgNodes)
			{
				// Find all image elements within each SVG
				var imageNodes = svgNode.SelectNodes(".//image");

				if (imageNodes != null)
				{
					foreach (var imageNode in imageNodes)
					{
						// Get the xlink:href attribute which contains the image filename
						var xlinkHref = imageNode.GetAttributeValue("xlink:href", null!);

						if (!string.IsNullOrEmpty(xlinkHref))
						{
							// Extract just the filename from the path if needed
							string filename = Path.GetFileName(xlinkHref);
							imageFilenames.Add(filename);
						}
					}
				}
			}
		}

		return imageFilenames;
	}

	static string GetMimeType(string fileName)
	{
		var fileExtension = Path.GetExtension(fileName);
		return fileExtension switch
		{
			".jpg" => jpg,
			".jpeg" => jpeg,
			".png" => png,
			".gif" => gif,
			".webp" => webp,
			_ => jpg
		};
	}
}
