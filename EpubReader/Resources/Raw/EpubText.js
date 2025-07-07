/**
 * Flag indicating if the previous page navigation occurred.
 * @type {boolean}
 */
let isPreviousPage = false;

/**
 * Reference to the iframe element that contains the EPUB content.
 * @type {HTMLIFrameElement|null}
 */
let frame = null;

/**
 * Number of columns to display in the EPUB layout.
 * @type {number}
 */
let colCount = 1;

/**
 * Detects the user's operating system platform based on the navigator user agent.
 * @returns {Object} An object with boolean flags for different platforms (isIOS, isMac, isWindows, isAndroid).
 */
function detectPlatform() {
    const userAgent = navigator.userAgent.toLowerCase();

    return {
        isIOS: /iphone|ipad|ipod/.test(userAgent) || (/mac/.test(userAgent) && navigator.maxTouchPoints > 1),
        isMac: /macintosh|mac os x/.test(userAgent) && !(/iphone|ipad|ipod/.test(userAgent)) && (navigator.maxTouchPoints < 1),
        isWindows: /win32|win64|windows|wince/.test(userAgent),
        isAndroid: /android/.test(userAgent)
    };
}

/**
 * Prevents images from moving down when font size changes
 * This function stabilizes image positioning while preserving multicolumn layout
 */
function fixImagePositioning() {
    if (!frame?.contentWindow?.document) return;

    const doc = frame.contentWindow.document;
    const images = doc.querySelectorAll('img, svg');

    // Add a style element with our image fixes if it doesn't exist
    if (!doc.getElementById('image-position-fixes')) {
        const styleEl = doc.createElement('style');
        styleEl.id = 'image-position-fixes';
        styleEl.textContent = `
            img, svg {
                max-width: 100%;
                height: auto !important;
                width: auto !important;
                max-height: 95vh;
                object-fit: contain;
                page-break-inside: avoid;
                break-inside: avoid;
            }
            
            /* For multicolumn layouts, ensure images respect column boundaries */
            body[style*="column-count"] img,
            body[style*="column-width"] img,
            html[style*="column-count"] img,
            html[style*="column-width"] img {
                max-width: 100%; 
                display: inline-block;
                vertical-align: top;
                margin: 0.5em 0;
            }
            
            /* Handle images in paragraphs and divs */
            p img, div img {
                vertical-align: top;
                margin: 0.2em 0;
            }
            
            /* Figure handling */
            figure {
                break-inside: avoid;
                page-break-inside: avoid;
                margin: 0.5em 0;
                max-width: 100%;
            }
            
            figcaption {
                font-size: 0.9em;
                margin-top: 0.3em;
            }
        `;
        doc.head.appendChild(styleEl);
    }

    // Process each image
    images.forEach(img => {
        // Skip images that have already been processed
        if (img.hasAttribute('data-positioned')) return;

        // Set fixed dimensions if we can determine them
        const naturalWidth = img.naturalWidth || img.width;
        const naturalHeight = img.naturalHeight || img.height;

        // If image has em/rem based dimensions, convert to fixed size
        if ((img.style.width && (img.style.width.includes('em') || img.style.width.includes('rem'))) ||
            (img.style.height && (img.style.height.includes('em') || img.style.height.includes('rem')))) {
            // Get computed dimensions
            const computedStyle = window.getComputedStyle(img);
            const computedWidth = parseInt(computedStyle.width);
            const computedHeight = parseInt(computedStyle.height);

            if (!isNaN(computedWidth)) img.width = computedWidth;
            if (!isNaN(computedHeight)) img.height = computedHeight;

            // Clear relative units
            if (img.style.width && (img.style.width.includes('em') || img.style.width.includes('rem')))
                img.style.width = 'auto';
            if (img.style.height && (img.style.height.includes('em') || img.style.height.includes('rem')))
                img.style.height = 'auto';
        }

        // Mark as processed
        img.setAttribute('data-positioned', 'true');

        // Add special handling for inline images
        const parent = img.parentElement;
        if (parent && (parent.tagName === 'P' || parent.tagName === 'DIV') &&
            !parent.classList.contains('image-container') &&
            !img.closest('figure')) {

            // Don't disrupt natural flow in multicolumn layout
            if (colCount > 1) {
                // For multicolumn, just ensure the image stays in place
                img.style.display = 'inline-block';
                img.style.verticalAlign = 'top';

                // Add a specific class to help with styling
                img.classList.add('multicolumn-image');
            }
        }
    });
}

/**
 * Main initialization function that runs when the DOM is fully loaded.
 * Sets up event listeners and initializes the EPUB reader interface.
 */
document.addEventListener("DOMContentLoaded", function () {
    frame = document.getElementById("page");
    const body = document.getElementById("body");
    const root = document.documentElement;
    const platform = detectPlatform();

    if (!frame || !body) {
        console.error("Required DOM elements (iframe with id 'page' or body with id 'body') not found.");
        return; // Exit if essential elements are missing
    }

    // Add platform-specific class to body
    if (platform.isWindows) {
        body.classList.add('windows-platform');
        frame.classList.add('windows-platform');
    }

    /**
     * Sets the dimensions of the iframe and body elements, and updates CSS custom properties.
     * Called on initial load and on resize events.
     */
    const setDimensions = () => {
        if (!frame.contentWindow) {
            console.warn("Iframe contentWindow is not available for dimension setting yet.");
            return;
        }
        const width = Math.floor(frame.contentWindow.innerWidth);
        const height = Math.floor(frame.contentWindow.innerHeight);

        frame.style.width = `${width}px`;
        body.style.width = `${width}px`;
        body.style.height = `${height}px`;
        frame.style.height = `${height}px`;

        root.style.setProperty('--root-width', `${width}px`);
        root.style.setProperty('--root-height', `${height}px`);
    };

    // Set initial dimensions and add a resize listener for the iframe's content
    setDimensions();
    frame.contentWindow?.addEventListener('resize', setDimensions);

    /**
     * Handles iframe load events. Ensures proper page layout, particularly for two-column mode
     * by adding a blank page if necessary to make page count even.
     */
    const originalOnload = frame.onload;
    frame.onload = function () {
        try {
            if (!frame.contentWindow?.document) {
                console.error("Cannot access iframe content - likely CORS restriction or iframe not fully loaded.");
                return;
            }

            // Fix image positioning when page loads
            setTimeout(fixImagePositioning, 100);

            // Early exit for mobile platforms as per original logic
            if (platform.isAndroid || platform.isIOS) {
                console.log("Not setting extra page for single column on mobile.");
                return;
            }

            /**
             * Calculates the number of pages within the iframe's content.
             * @returns {number} The calculated page count based on content width.
             */
            const getPageCount = () => {
                const innerWidth = frame.contentWindow?.innerWidth ?? 0;
                const scrollWidth = frame.contentWindow?.document.documentElement.scrollWidth ?? 0;
                return Math.floor(scrollWidth / innerWidth);
            };

            const pageCount = getPageCount();

            // Add a blank page if the page count is odd
            if (pageCount % 2 !== 0) {
                const blankPage = frame.contentWindow.document.createElement("div");
                const { width, height } = frame.style; // Use dimensions from the iframe itself

                blankPage.style.cssText = `
                    width: ${width};
                    height: ${height};
                    display: inline-block;
                    background-color: transparent;
                    overflow: hidden;
                `;
                if (colCount > 1) {
                    frame.contentWindow.document.body.appendChild(blankPage);
                    console.log(`Added blank page to make page count even: ${pageCount + 1}`);
                }
            } else {
                console.log(`Page count is already even: ${pageCount}`);
            }

        } catch (error) {
            console.error("Error during iframe onload:", error);
        } finally {
            // Notify via URL change after the iframe content has loaded and processed
            window.location.href = 'https://runcsharp.pageLoad?true';
        }
    };

    /**
     * Checks if the horizontal scroll position within the iframe is at the start (leftmost position).
     * @returns {boolean} True if at the start, false otherwise.
     */
    const isHorizontalScrollAtStart = () => {
        if (!frame.contentWindow) {
            console.warn("Iframe contentWindow is null for scroll check.");
            return false;
        }
        return frame.contentWindow.scrollX <= 0;
    };

    /**
     * Checks if the horizontal scroll position within the iframe is at the end (rightmost position).
     * @returns {boolean} True if at the end, false otherwise.
     */
    const isHorizontallyScrolledToEnd = () => {
        if (!frame.contentWindow) {
            return false;
        }
        const contentDoc = frame.contentWindow.document.documentElement;
        const maxScrollLeft = contentDoc.scrollWidth - contentDoc.clientWidth;
        // Allow a small tolerance (30px) for floating point inaccuracies
        return Math.abs(frame.contentWindow.scrollX - maxScrollLeft) <= 30;
    };

    /**
     * Scrolls the iframe content to the left by one page width.
     * Accounts for column gap in the calculation.
     */
    const scrollLeft = () => {
        if (!frame.contentWindow) return;
        // Retrieve column-gap from the iframe's document, defaulting to 0 if not found
        const gap = parseInt(window.getComputedStyle(frame.contentWindow.document.documentElement).getPropertyValue("column-gap")) || 0;
        const scrollAmount = frame.contentWindow.innerWidth + gap;

        if (platform.isWindows) {
            frame.contentWindow.scrollTo(frame.contentWindow.scrollX - scrollAmount, 0);
        } else {
            frame.contentWindow.scrollTo({ left: frame.contentWindow.scrollX - scrollAmount, top: 0, behavior: "smooth" });
        }
    };

    /**
     * Scrolls the iframe content to the right by one page width.
     * Accounts for column gap in the calculation.
     */
    const scrollRight = () => {
        if (!frame.contentWindow) return;
        // Retrieve column-gap from the iframe's document, defaulting to 0 if not found
        const gap = parseInt(window.getComputedStyle(frame.contentWindow.document.documentElement).getPropertyValue("column-gap")) || 0;
        const scrollAmount = frame.contentWindow.innerWidth + gap;

        if (platform.isWindows) {
            frame.contentWindow.scrollTo(frame.contentWindow.scrollX + scrollAmount, 0);
        } else {
            frame.contentWindow.scrollTo({ left: frame.contentWindow.scrollX + scrollAmount, top: 0, behavior: "smooth" });
        }
    };

    /**
     * Validates the origin of incoming messages for security purposes.
     * @param {string} origin - The origin of the incoming message.
     * @returns {boolean} True if the origin is allowed, false otherwise.
     */
    function validateOrigin(origin) {
        const isAllowedOrigin =
            ((platform.isAndroid || platform.isWindows) && origin === "https://demo") ||
            ((platform.isIOS || platform.isMac) && origin === "app://demo");

        if (!isAllowedOrigin) {
            console.warn("Received message from unauthorized origin:", origin);
        }

        return isAllowedOrigin;
    }

    // Listen for messages from the parent window
    window.addEventListener("message", function (event) {
        // Origin validation for security
        if (!validateOrigin(event.origin)) {
            return;
        }
        const { data } = event; // Destructure event.data for conciseness

        if (data.startsWith("jump.")) {
            const href = data.substring(5);
            console.log("Jumping to:", href);
            window.location.href = `https://runcsharp.jump?${href}`;
        } else if (data === "next") {
            if (isHorizontallyScrolledToEnd()) {
                console.log("Reached end of current content, requesting next page.");
                window.location.href = 'https://runcsharp.next?true';
            } else {
                scrollRight();
            }
        } else if (data === "prev") {
            if (isHorizontalScrollAtStart()) {
                console.log("Reached start of current content, requesting previous page.");
                window.location.href = 'https://runcsharp.prev?true';
            } else {
                scrollLeft();
            }
        } else if (data === "menu") {
            console.log("Received menu command.");
            window.location.href = 'https://runcsharp.menu?true';
        }
    });
});


/**
 * Retrieves the width of the content in the iframe.
 * @returns {number} The inner width of the iframe content window, or 0 if unavailable.
 */
function getWidth() {
    if (!frame?.contentWindow) {
        console.warn("Iframe contentWindow not available in getWidth.");
        return 0; // Return 0 or handle error appropriately
    }
    return Math.floor(frame.contentWindow.innerWidth);
}

/**
 * Calculates the number of pages within the iframe based on content width.
 * @returns {number} The calculated number of pages, or 0 if the iframe is not available.
 */
function getPageCount() {
    if (!frame?.contentWindow) {
        console.warn("Iframe contentWindow not available in getPageCount.");
        return 0; // Return 0 or handle error appropriately
    }
    const width = Math.floor(frame.contentWindow.innerWidth);
    const containerWidth = Math.abs(frame.contentWindow.document.documentElement.scrollWidth);
    const pages = Math.floor(containerWidth / width);
    return pages;
}

/**
 * Navigates to the end of the iframe content if the previous page flag is set.
 * Used when navigating backwards through pages to position at the end of the previous page.
 */
function gotoEnd() {
    if (isPreviousPage) {
        scrollToHorizontalEnd();
        isPreviousPage = false;
    }
}

/**
 * Sets a flag indicating that navigation to the previous page has occurred.
 * This flag is used by gotoEnd() to determine if scrolling to the end is needed.
 */
function setPreviousPage() {
    isPreviousPage = true;
}

/**
 * Loads a specified URL into the iframe.
 * @param {string} page - The URL to load in the iframe.
 * @returns {boolean} True if the frame was found and source set, false otherwise.
 */
function loadPage(page) {
    if (!frame) {
        console.error("Frame not found for loadPage.");
        return false;
    }
    console.log("Frame found. Loading page:", page);
    frame.setAttribute('src', page);
    return true;
}

/**
 * Scrolls the iframe content to its horizontal end (rightmost position).
 * Handles cases where the iframe content might not be fully loaded yet by using the onload event.
 */
function scrollToHorizontalEnd() {
    if (!frame) {
        console.error("Iframe element not found for scrollToHorizontalEnd.");
        return;
    }

    // Check if contentWindow and its document are ready
    if (frame.contentWindow && frame.contentWindow.document.readyState === 'complete') {
        const contentDoc = frame.contentDocument || frame.contentWindow.document;
        // Correct calculation for max scrollable left
        const maxScrollLeft = contentDoc.documentElement.scrollWidth - contentDoc.documentElement.clientWidth;
        console.log("Scrolling to end of container.");
        frame.contentWindow.scrollTo(maxScrollLeft, 0);
    } else {
        // If iframe not ready, set onload to call this function again
        frame.onload = function () {
            scrollToHorizontalEnd(); // Recurse when loaded
            frame.onload = null; // Remove listener to prevent multiple calls
        };
    }
}

/**
 * Sets a CSS custom property on the iframe's document element.
 * @param {string} property - The name of the CSS custom property (e.g., '--my-color').
 * @param {string} value - The value to set for the property.
 */
function setReadiumProperty(property, value) {
    const root = frame?.contentWindow?.document?.documentElement; // Safely access properties
    if (root) {
        console.log(`Setting iframe CSS property: ${property} = ${value}`);
        root.style.setProperty(property, value);
        if (property === '--USER__colCount') {
            colCount = parseInt(value);
            // Fix image positioning after column count changes
            setTimeout(fixImagePositioning, 100);
        }

        // Fix image positioning after font size changes
        if (property.includes('fontSize') || property.includes('font-size')) {
            setTimeout(fixImagePositioning, 100);
        }
    } else {
        console.warn(`Could not set property '${property}'. Iframe content not accessible or not loaded.`);
    }
}

/**
 * Removes a CSS custom property from the iframe's document element.
 * @param {string} property - The name of the CSS custom property to remove.
 */
function unsetReadiumProperty(property) {
    const root = frame?.contentWindow?.document?.documentElement; // Safely access properties
    if (root) {
        console.log(`Unsetting iframe CSS property: ${property}`);
        root.style.removeProperty(property);
    } else {
        console.warn(`Could not unset property '${property}'. Iframe content not accessible or not loaded.`);
    }
}

/**
 * Sets the background color CSS custom property on the main document element (outside the iframe).
 * @param {string} color - The color value to set (e.g., "red", "#HEX", "rgb(R,G,B)").
 */
function setBackgroundColor(color) {
    if (color == null || color === '') {
        console.log("No color provided, unsetting background color.");
        document.documentElement.style.removeProperty('--background-color');
        return;
    }
    console.log(`Setting background color to: ${color}`);
    document.documentElement.style.setProperty('--background-color', color);
}

/**
 * Removes the background color CSS custom property from the main document element.
 */
function unsetBackgroundColor() {
    document.documentElement.style.removeProperty('--background-color');
}