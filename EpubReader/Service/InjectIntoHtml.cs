using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using EpubReader.Models;

namespace EpubReader.Service;

public static partial class InjectIntoHtml
{
	[GeneratedRegex(@"<p(\s[^>]*)?>", RegexOptions.None, matchTimeoutMilliseconds: 20000)]
	private static partial Regex HasParagraphs();

	[GeneratedRegex(@"\s*height\s*=\s*""100%""", RegexOptions.None, matchTimeoutMilliseconds: 20000)]
	private static partial Regex CleanedTag();

	[GeneratedRegex(@"<style[^>]*?>[\s\S]*?</style>|<style[^>]*?/>", RegexOptions.None, matchTimeoutMilliseconds: 20000)]
	private static partial Regex WithoutStyles();

	[GeneratedRegex(@"<script[^>]*?>[\s\S]*?</script>|<script[^>]*?/>", RegexOptions.None, matchTimeoutMilliseconds: 20000)]
	private static partial Regex WithoutScripts();

	[GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 20000)]
	private static partial Regex StyleTagRegex();

	static readonly TimeSpan regexTimeout = TimeSpan.FromSeconds(20);
	static string jpg => "image/jpeg";
	static string png => "image/png";
	static string gif => "image/gif";
	static string webp => "image/webp";
	public static string UpdateHtml(string html, Book book, Settings settings)
	{
		if (string.IsNullOrEmpty(html))
		{
			return string.Empty;
		}
		html = FixImageTags(html);
		html = RemoveScriptAndStyleTags(html);
		html = StyleTagRegex().Replace(html, string.Empty);
		html = InjectCss(html, book, settings);
		html = ReplaceImageUrls(html, book.Images);
		html = InjectJavascript(html, disableScroll + buttonNavigation + adjustTextSizeAndStyle + adjustFontSize + adjustSVGImages);
		html = AddDivContainer(html);
		return html;
	}

	static bool HasParagraphsRegex(string htmlString)
	{
		// This pattern looks for opening <p> tags with optional attributes
		return HasParagraphs().IsMatch(htmlString);
	}

	static string FixImageTags(string inputString)
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
		}, RegexOptions.None, regexTimeout);

		if (string.IsNullOrEmpty(result))
		{
			return inputString;
		}
		return result;
	}
	static string InjectCss(string html, Book book, Settings settings)
	{
		int numberOfColumns = 1;
		if ((OperatingSystem.IsWindows() || OperatingSystem.IsMacCatalyst()) && HasParagraphsRegex(html))
		{
			numberOfColumns = 2;
		}

		var css = new StringBuilder(GetStyle(numberOfColumns));
		if (!HasParagraphsRegex(html))
		{
			css.Append(imageStyle);
		}

		foreach (var cssFile in book.Css)
		{
			var filteredCSS = FilterCalibreCss(cssFile.Content);
			filteredCSS = RemoveCssProperties(filteredCSS);
			css.Append(ReplaceImageUrls(filteredCSS, book.Images));
		}
		
		var styleTag = new StringBuilder();
		styleTag.Append(GenerateCSSFromString(settings));
		styleTag.Append(css);

		int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
		if (headEndTagIndex >= 0)
		{
			html = html.Insert(headEndTagIndex, $"<style>{styleTag}</style>");
		}

		return html;
	}

	static string FilterCalibreCss(string? cssString)
	{
		if (string.IsNullOrEmpty(cssString))
		{
			return string.Empty;
		}
		string regexPattern = @"\.calibre(1)?\s*\{[^}]*\}";
		Regex regex = new(regexPattern, RegexOptions.None, regexTimeout);

		return regex.Replace(cssString, string.Empty); // Replace matches with empty string
	}

	static string RemoveCssProperties(string htmlString)
	{
		// Generated Regex for matching CSS rules
		var cssRuleRegex = new Regex(@"((?:p|body|html)(?:\s*,\s*(?:p|body|html))*)(\s*\{[^}]*\})", RegexOptions.IgnoreCase | RegexOptions.Singleline, regexTimeout);

		// Generated Regex for removing margin, padding, and text-indent
		var propertyRemovalRegex = new Regex(@"(^|\s|\;)\s*(margin|padding|text-indent)\s*:[^;]*;?", RegexOptions.IgnoreCase, regexTimeout);

		// Generated Regex for cleaning up semicolons and whitespace
		var semicolonCleanupRegex = new Regex(@"\s*;\s*}", RegexOptions.Compiled, regexTimeout);
		var emptyBlockCleanupRegex = new Regex(@"{\s*}", RegexOptions.Compiled, regexTimeout);
		var multipleSpaceCleanupRegex = new Regex(@"\s{2,}", RegexOptions.Compiled, regexTimeout);

		return cssRuleRegex.Replace(htmlString, match =>
		{
			string selectors = match.Groups[1].Value;
			string cssBlock = match.Groups[2].Value;

			cssBlock = propertyRemovalRegex.Replace(cssBlock, "$1");
			cssBlock = semicolonCleanupRegex.Replace(cssBlock, " }");
			cssBlock = emptyBlockCleanupRegex.Replace(cssBlock, "{ }");
			cssBlock = multipleSpaceCleanupRegex.Replace(cssBlock, " ");

			return selectors + cssBlock;
		});
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
               	{(string.IsNullOrEmpty(settings.FontFamily) ? "" : $"font-family: {settings.FontFamily};")}
            }}
			p {{ 
				{(settings.FontSize > 0 ? $"font-size: {settings.FontSize}px !important;" : "")}
			}}";
	}

	static string ReplaceImageUrls(string? inputString, List<Models.Image> images)
	{
		if (string.IsNullOrEmpty(inputString))
		{
			return string.Empty;
		}
		foreach (var image in images)
		{
			inputString = ReplaceImageUrl(inputString, image.FileName, image.ImageUrl);
		}
		return inputString;
	}

	static string ReplaceImageUrl(string inputString, string imageName, string imageString)
	{	
		var base64String = $"data:{GetMimeType(imageName)};base64,{imageString}";

		// For HTML encoded version
		var htmlEncodedString = HtmlEncoder.Default.Encode(imageString);
		var htmlEncodedBase64String = $"data:{GetMimeType(imageName)};base64,{htmlEncodedString}";

		string escapedImageName = Regex.Escape(Path.GetFileNameWithoutExtension(imageName));
		string escapedFullImageName = Regex.Escape(imageName);

		// HTML img tag pattern
		string imgPattern = $@"<img[^>]*src=[""']([^""']*{escapedFullImageName}[^""']*)[""'][^>]*>";
		string imgPattern2 = @"<img[^>]*src=[""']([^""']*)[""'][^>]*>";

		// SVG image pattern - improved to better match XML
		string svgPattern = $@"<image[^>]*xlink:href=[""']([^""']*{escapedFullImageName})[""'][^>]*>";

		// Generic SVG pattern to catch other cases
		string svgGenericPattern = @"<image[^>]*xlink:href=[""']([^""']*)[""'][^>]*>";

		// CSS pattern
		string patternCss = $@"background:\s*url\(\s*['""]?([^'""]*/)*{escapedImageName}(\.[a-zA-Z]+)?['""]?\s*\)\s*(no-repeat\s*50%\s*)?;";
		string replacement = $"background-image: url({base64String});\nbackground-repeat: no-repeat;\nbackground-position: 50%;";

		// Replace CSS backgrounds
		inputString = Regex.Replace(inputString, patternCss, replacement, RegexOptions.IgnoreCase, regexTimeout);

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
		}, RegexOptions.None, regexTimeout);

		inputString = Regex.Replace(inputString, imgPattern2, match =>
		{
			string originalTag = match.Value;
			string fileName = ExtractFilenameFromImgTag(originalTag);

			if (!IsMatchingFilename(fileName, imageName))
			{
				return originalTag;
			}

			return originalTag.Replace(match.Groups[1].Value, htmlEncodedBase64String);
		}, RegexOptions.None, regexTimeout);

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
	static string InjectJavascript(string html, string javascript)
	{
		int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
		if (headEndTagIndex >= 0)
		{
			html = html.Insert(headEndTagIndex, $@"<script>{javascript}</script>");
		}

		return html;
	}
	static string RemoveScriptAndStyleTags(string htmlString)
	{
		if (string.IsNullOrEmpty(htmlString))
		{
			return htmlString;
		}

		// Remove script tags with all their attributes and content
		string withoutScripts = WithoutScripts().Replace(htmlString, string.Empty);

		// Remove style tags with all their attributes and content
		string withoutStyles = WithoutStyles().Replace(withoutScripts, string.Empty);

		// Return the cleaned string
		return withoutStyles.Trim();
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
	static string GetMimeType(string fileName)
	{
		var fileExtension = Path.GetExtension(fileName);
		return fileExtension switch
		{
			".jpg" => jpg,
			".jpeg" => jpg,
			".png" => png,
			".gif" => gif,
			".webp" => webp,
			_ => jpg
		};
	}

	static string GetStyle(int columns)
	{
		return $@"
    ::-webkit-scrollbar {{
        display: none;
    }}

    * {{
        -webkit-touch-callout: none;
    }}
	
    #scrollContainer {{
        columns: {columns};
        overflow-x: auto;
		margin-top: 1em;
        height: 95vh;
		
    }}

    #scrollContainer p, h1, h2, h3, h4 {{
        text-align: justify;
		margin-left: 1em;
        margin-right: 1em;
    }}";
	}

	static readonly string disableScroll = @"
        window.addEventListener('wheel', function(event) {
            event.preventDefault();
        }, { passive: false });

        window.addEventListener('touchmove', function(event) {
            event.preventDefault();
        }, { passive: false });";

	static readonly string adjustFontSize = @"
		function changeTextStyle(fontSize) {
			// Select all paragraphs and spans in the document
			const textElements = document.querySelectorAll('p');
	
			// Apply the styles to each element
			textElements.forEach(element => {
			  // Set font size if provided
			  if (fontSize) {
				element.style.setProperty('font-size', fontSize + 'px', 'important');
			  }
			});
		}";

	static readonly string imageStyle = @"
		.image_full {
		text-align: center;
		}
    
		.image_full img {
		  display: block;
		  margin: 0 auto;
		  max-width: 100%;
		  height: 100vh;
		}
    
		/* New CSS for cover_image */
		.cover_image {
		  text-align: center;
		}
    
		.cover_image img {
		  display: block;
		  margin: 0 auto;
		  max-width: 100%;
		  height: 100vh;
		}

		/* Optional: if you need to set the image to inline-block */
		.cover-image img {
		  display: inline-block;
		}
		img {
		  max-width: 100vw; /* Ensures the image doesn't exceed the page width */
		  height: 100vh; /* Maintains aspect ratio by scaling height proportionally */
		  display: block; /* Removes extra space below inline images */
		}";

	static readonly string adjustTextSizeAndStyle = @"
		/**
		* Apply multiple styles to an element
		* @param {Object} options - Style options to apply
		* @param {string|number} [options.fontSize] - Font size to apply
		* @param {string} [options.backgroundColor] - Background color to apply
		* @param {string} [options.textColor] - Text color to apply
		* @param {string} [options.fontFamily] - Font family to apply
		* @param {HTMLElement|string} [target='body'] - Target element or selector
		* @returns {boolean} - True if successful, false if failed
		*/
		function applyStyles(options = {}, target = 'body') {
		try {
			// Find the target element if a selector string was provided
			let element = target;
			if (typeof target === 'string') {
				element = document.querySelector(target);
			}
    
			// Make sure element exists
			if (!element) {
				console.error('Target element not found:', target);
				return false;
			}
    
			// Apply font size if provided (using different methods to ensure it works)
			if (options.fontSize !== undefined) {
				let fontSize = options.fontSize;
        
				// Convert to string with px if it's a number
				if (typeof fontSize === 'number') {
					fontSize = fontSize + 'px';
				}
        
				// Add px if it's just a number as string
				if (/^\d+$/.test(fontSize)) {
					fontSize = fontSize + 'px';
				}
        
				// Method 1: Using setProperty with !important flag
				element.style.setProperty('font-size', fontSize, 'important');
        
				// Method 2: Using inline style attribute with !important
				const currentStyles = element.getAttribute('style') || '';
				const fontSizePattern = /font-size\s*:\s*[^;]+;?/g;
				const newStyles = currentStyles.replace(fontSizePattern, '');
				element.setAttribute('style', `${newStyles} font-size: ${fontSize} !important;`);
        
				// Method 3: Add a custom stylesheet rule with highest specificity
				let styleSheet = document.getElementById('custom-styles');
				if (!styleSheet) {
					styleSheet = document.createElement('style');
					styleSheet.id = 'custom-styles';
					document.head.appendChild(styleSheet);
				}
        
				// Create a high-specificity selector for the element
				let selector;
				if (target === 'body') {
					selector = 'body';
				} else if (element.id) {
					selector = `#${element.id}`;
				} else if (element.className) {
					// Convert class list to a high-specificity selector
					selector = '.' + element.className.split(' ').join('.');
				} else {
					// Create a unique ID if there's no good selector
					const uniqueId = 'custom-styled-' + Math.random().toString(36).substr(2, 9);
					element.id = uniqueId;
					selector = `#${uniqueId}`;
				}
        
				// Add the rule to the stylesheet
				const cssRule = `${selector} { font-size: ${fontSize} !important; }`;
				styleSheet.textContent += cssRule;
			}
    
			// Apply background color if provided
			if (options.backgroundColor !== undefined) {
				element.style.setProperty('background-color', options.backgroundColor, 'important');
			}
    
			// Apply text color if provided
			if (options.textColor !== undefined) {
				element.style.setProperty('color', options.textColor, 'important');
			}
    
			// Apply font family if provided
			if (options.fontFamily !== undefined) {
				element.style.setProperty('font-family', options.fontFamily, 'important');
			
				// Also apply font family using the custom stylesheet for maximum specificity
				let styleSheet = document.getElementById('custom-styles');
				if (!styleSheet) {
					styleSheet = document.createElement('style');
					styleSheet.id = 'custom-styles';
					document.head.appendChild(styleSheet);
				}
			
				// Use the same selector logic as for font size
				let selector;
				if (target === 'body') {
					selector = 'body';
				} else if (element.id) {
					selector = `#${element.id}`;
				} else if (element.className) {
					selector = '.' + element.className.split(' ').join('.');
				} else {
					const uniqueId = element.id || ('custom-styled-' + Math.random().toString(36).substr(2, 9));
					if (!element.id) element.id = uniqueId;
					selector = `#${uniqueId}`;
				}
			
				// Add the font-family rule to the stylesheet
				const cssRule = `${selector} { font-family: ${options.fontFamily} !important; }`;
				styleSheet.textContent += cssRule;
			}
    
			return true;
		} catch (error) {
			console.error('Error applying styles:', error);
			return false;
		}
	}";

	static readonly string adjustSVGImages = @"
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
    
		// Ensure body and inputString are set to use full viewport
		document.body.style.margin = '0';
		document.body.style.padding = '0';
		document.body.style.width = '100%';
		document.body.style.height = '100vh';
		document.documentElement.style.width = '100%';
		document.documentElement.style.height = '100%';
		}";

	static readonly string buttonNavigation = @"
        function nextPage() {
            document.getElementById(""scrollContainer"").scrollLeft += window.visualViewport.width;
        }

        function prevPage() {
            document.getElementById(""scrollContainer"").scrollLeft -= window.visualViewport.width;
        }
		
		function scrollToEnd() {
			const scrollContainer = document.getElementById(""scrollContainer"");
    
			if (!scrollContainer) {
				console.error('scrollContainer element not found');
				return;
			}
    
			// Alternative method: get all columns and scroll to the last one
			const totalWidth = scrollContainer.scrollWidth;
			const viewportWidth = scrollContainer.clientWidth;
    
			// Force scroll to the maximum possible position
			scrollContainer.scrollLeft = 999999; // Large value forces scroll to end
    
			// Log for debugging
			console.log(`Total width: ${totalWidth}, Viewport: ${viewportWidth}, Max scroll: ${totalWidth - viewportWidth}`);
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