namespace EpubReader.Util;

public static class JavaScriptConstants
{
	/// <summary>
	/// Disable scrolling on the web view.
	/// </summary>
	public static readonly string DisableScroll = @"
        window.addEventListener('wheel', function(event) {
            event.preventDefault();
        }, { passive: false });

        window.addEventListener('touchmove', function(event) {
            event.preventDefault();
        }, { passive: false });";

	/// <summary>
	/// Adjust the font size of the text in the web view.
	/// </summary>
	public static readonly string AdjustFontSize = @"
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

	/// <summary>
	/// Adjust the text size and style in the web view.
	/// </summary>
	public static readonly string AdjustTextSizeAndStyle = @"
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
				if (/^\d+$/.image(fontSize)) {
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

	/// <summary>
	/// Adjusts the SVG to fit the screen.
	/// </summary>
	public static readonly string AdjustSVGImages = @"
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

	/// <summary>
	/// JavaScript function for button navigation.
	/// </summary>
	public static readonly string ButtonNavigation = @"
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
