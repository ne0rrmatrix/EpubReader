using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using EpubReader.Models;

namespace EpubReader.Service;

public static partial class InjectIntoHtml
{
	static readonly TimeSpan regexTimeout = TimeSpan.FromSeconds(20);
	static string jpg => "image/jpeg";
	static string png => "image/png";
	static string gif => "image/gif";
	public static string InjectAllCss(string html, Book book, Settings settings)
	{
		if (string.IsNullOrEmpty(html))
		{
			return string.Empty;
		}

		var cssContent = BuildCssContent(html, book, settings);
		html = RemoveExistingStyleTags(html);
		html = InjectCss(html, cssContent);
		html = ReplaceImageUrls(html, book.Images);
		html = InjectJavascript(html, disableScroll + jsButtons);
		html = AddDivContainer(html);

		return html;
	}

	static string BuildCssContent(string html, Book book, Settings settings)
	{
		var css = new StringBuilder(style);
		var cssList = ExtractCssFileNames(html);

		foreach (var cssFile in cssList.Select(item => book.Css.Find(x => item.Contains(x.FileName))))
		{
			css.Append(ReplaceCssImageUrls(cssFile?.Content, book.Images));
		}

		var styleTag = new StringBuilder();
		styleTag.Append(GenerateCSSFromString(settings));
		styleTag.Append(css);

		return styleTag.ToString();
	}

	static List<string> ExtractCssFileNames(string html)
	{
		const string pattern = @"<link\s+href=""([^""]+\.css)""\s+rel=""stylesheet""";
		var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase, regexTimeout);

		return [.. matches.Select(match => Path.GetFileName(match.Groups[1].Value))];
	}

	static string GenerateCSSFromString(Settings settings)
	{
		if (string.IsNullOrEmpty(settings.BackgroundColor) && string.IsNullOrEmpty(settings.TextColor) && settings.FontSize <= 0 && string.IsNullOrEmpty(settings.FontFamily))
		{
			return string.Empty;
		}

		return $@"
            body {{
                {(string.IsNullOrEmpty(settings.BackgroundColor) ? "" : $"background-color: {settings.BackgroundColor};")}
                {(string.IsNullOrEmpty(settings.TextColor) ? "" : $"color: {settings.TextColor};")}
                {(settings.FontSize > 0 ? $"font-size: {settings.FontSize}px !important;" : "")}
                {(string.IsNullOrEmpty(settings.FontFamily) ? "" : $"font-family: {settings.FontFamily};")}
            }}";
	}

	static string InjectCss(string html, string css)
	{
		int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
		if (headEndTagIndex >= 0)
		{
			html = html.Insert(headEndTagIndex, $"<style>{css}</style>");
		}
		else
		{
			html = $"<style>{css}</style>" + html;
		}
		return html;
	}
	static string ReplaceCssImageUrls(string? css, List<Models.Image> images)
	{
		if (string.IsNullOrEmpty(css))
		{
			return string.Empty;
		}
		foreach (var image in images)
		{
			css = ReplaceCssImageUrl(css, image.FileName, image.ImageUrl);
		}
		return css;
	}

	public static string ReplaceCssImageUrl(string css, string imageName, string base64String)
	{
		base64String = $"data:{GetMimeType(imageName)};base64,{base64String}";
		string escapedImageName = Regex.Escape(Path.GetFileNameWithoutExtension(imageName));
		string pattern = $@"background:\s*url\(\s*['""]?([^'""]*/)*{escapedImageName}(\.[a-zA-Z]+)?['""]?\s*\)\s*(no-repeat\s*50%\s*)?;";
		string replacement = $"background-image: url({base64String});\nbackground-repeat: no-repeat;\nbackground-position: 50%;";
		string modifiedCss = Regex.Replace(css, pattern, replacement, RegexOptions.IgnoreCase);
		return modifiedCss;
	}

	static string ReplaceImageUrls(string html, List<Models.Image> images)
	{
		foreach (var image in images)
		{
			html = ReplaceImageUrl(html, image.FileName, image.ImageUrl);
		}
		return html;
	}

	static string ReplaceImageUrl(string htmlContent, string imageName, string newImageSource)
	{
		newImageSource = HtmlEncoder.Default.Encode(newImageSource);
		newImageSource = $"data:{GetMimeType(imageName)};base64,{newImageSource}";
		string imgPattern = $@"<img[^>]*src=[""']([^""']*{imageName}[^""']*)[""'][^>]*>";
		htmlContent = Regex.Replace(htmlContent, imgPattern, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, newImageSource);
		}, RegexOptions.None, regexTimeout);

		string imgPattern2 = @"<img[^>]*src=[""']([^""']*)[""'][^>]*>"; // For other img tags

		htmlContent = Regex.Replace(htmlContent, imgPattern2, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, newImageSource);
		}, RegexOptions.None, regexTimeout);


		string svgPattern = $@"<image[^>]*xlink:href=[""']([^""']*{imageName}[^""']*)[""'][^>]*>";
		
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
			html = $"<script>{javascript}</script>" + html;
		}
		return html;
	}

	static string AddDivContainer(string html)
	{
		if (string.IsNullOrEmpty(html))
		{
			return html;
		}

		const string openingBodyRegex = @"<body\s*([^>]*)>";
		const string closingBodyRegex = @"</body>";

		var openingBodyMatch = Regex.Match(html, openingBodyRegex, RegexOptions.IgnoreCase, regexTimeout);
		var closingBodyMatch = Regex.Match(html, closingBodyRegex, RegexOptions.IgnoreCase, regexTimeout);

		if (!openingBodyMatch.Success || !closingBodyMatch.Success)
		{
			return html;
		}

		var result = new StringBuilder();
		result.Append(html.AsSpan(0, openingBodyMatch.Index + openingBodyMatch.Length));
		result.Append("<div id=\"scrollContainer\">");
		result.Append(html.AsSpan(openingBodyMatch.Index + openingBodyMatch.Length, closingBodyMatch.Index - (openingBodyMatch.Index + openingBodyMatch.Length)));
		result.Append("</div></body>");
		result.Append(html.AsSpan(closingBodyMatch.Index + closingBodyMatch.Length));

		return result.ToString();
	}
	public static string GetMimeType(string fileName)
	{
		var fileExtension = Path.GetExtension(fileName);
		return fileExtension switch
		{
			".jpg" => jpg,
			".jpeg" => jpg,
			".png" => png,
			".gif" => gif,
			_ => jpg
		};
	}
	static string RemoveExistingStyleTags(string html)
	{
		return StyleTagRegex().Replace(html, string.Empty);
	}

	[GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 20000)]
	private static partial Regex StyleTagRegex();

	[GeneratedRegex("font-size:\\s*\\d+px\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontSizeRegex();

	[GeneratedRegex("font-family:\\s*[^;]+?\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontFamilyRegex();

	static readonly string disableScroll = @"
        window.addEventListener('wheel', function(event) {
            event.preventDefault();
        }, { passive: false });

        window.addEventListener('touchmove', function(event) {
            event.preventDefault();
        }, { passive: false });";

	static readonly string style = @"
        ::-webkit-scrollbar {
            display: none;
        }

        * {
            -webkit-touch-callout: none;
        }

        #scrollContainer {
            columns: 1;
            overflow-x: auto;
            height: 100vh;
        }

        #scrollContainer p {
            text-align: justify;
            margin-left: 2em;
            margin-right: 2em;
        }";

	static readonly string jsButtons = @"
		window.addEventListener('load', () => adjustSvgToScreen(true));

		function adjustSvgToScreen(preserveAspect = true) {
		// Get the SVG element
		const svg = document.querySelector('svg');
    
		if (!svg) return;
    
		// Set appropriate preserveAspectRatio
		if (preserveAspect) {
			// 'xMidYMid meet' maintains aspect ratio and centers the image
			svg.setAttribute('preserveAspectRatio', 'xMidYMid meet');
		}
    
		// Make sure the container div takes full available space
		const container = svg.parentElement;
		container.style.width = '100%';
		container.style.height = '100%';
		container.style.display = 'flex';
		container.style.justifyContent = 'center';
		container.style.alignItems = 'center';
    
		// Ensure body and html are set to use full viewport
		document.body.style.margin = '0';
		document.body.style.padding = '0';
		document.body.style.width = '100%';
		document.body.style.height = '100vh';
		document.documentElement.style.width = '100%';
		document.documentElement.style.height = '100%';
	}
        function nextPage() {
            document.getElementById(""scrollContainer"").scrollLeft += window.visualViewport.width;
        }

        function prevPage() {
            document.getElementById(""scrollContainer"").scrollLeft -= window.visualViewport.width;
        }

        function isHorizontalScrollAtStart() {
            var element = document.getElementById(""scrollContainer"");
            if (!element) {
                return false;
            }
            return element.scrollLeft === 0;
        }

        function isHorizontallyScrolledToEnd() {
            var element = document.getElementById(""scrollContainer"");
            if (!element) {
                return false;
            }
            const maxScrollLeft = element.scrollWidth - element.clientWidth;
            return Math.abs(element.scrollLeft - maxScrollLeft) <= 1;
        }";
}