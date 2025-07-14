/**
 * EPUB Reader JavaScript Interface
 * Handles rendering, navigation, and styling of EPUB content
 */

// Global state
let isPreviousPage = false;
let currentPage = 0;
let frame = null;
let colCount = 1;

/**
 * DOM and Platform Utilities
 */
const domUtils = {
    /**
     * Safely gets the iframe document
     * @returns {Document|null} The iframe document or null
     */
    getIframeDocument() {
        return frame?.contentWindow?.document || null;
    },

    /**
     * Safely gets the iframe content window
     * @returns {Window|null} The iframe content window or null
     */
    getContentWindow() {
        return frame?.contentWindow || null;
    },

    /**
     * Detects the user's operating system platform
     * @returns {Object} Platform flags
     */
    detectPlatform() {
        const userAgent = navigator.userAgent.toLowerCase();

        return {
            isIOS: /iphone|ipad|ipod/.test(userAgent) || (/mac/.test(userAgent) && navigator.maxTouchPoints > 1),
            isMac: /macintosh|mac os x/.test(userAgent) && !(/iphone|ipad|ipod/.test(userAgent)) && (navigator.maxTouchPoints < 1),
            isWindows: /win32|win64|windows|wince/.test(userAgent),
            isAndroid: /android/.test(userAgent)
        };
    }
};

/**
 * Scrolling and navigation utilities
 */
const navigationUtils = {
    /**
     * Checks if horizontal scroll is at the start
     * @returns {boolean} True if at start
     */
    isHorizontalScrollAtStart() {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) {
            console.warn("Iframe contentWindow is null for scroll check.");
            return false;
        }
        return contentWindow.scrollX <= 0;
    },

    /**
     * Checks if horizontal scroll is at the end
     * @returns {boolean} True if at end
     */
    isHorizontallyScrolledToEnd() {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) return false;

        const contentDoc = contentWindow.document.documentElement;
        const maxScrollLeft = contentDoc.scrollWidth - contentDoc.clientWidth;
        // Allow a small tolerance (30px) for floating point inaccuracies
        return Math.abs(contentWindow.scrollX - maxScrollLeft) <= 30;
    },

    /**
     * Scrolls the content left by one page
     * @param {Object} platform - Platform flags
     */
    scrollLeft(platform) {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) return;

        const scrollAmount = this.calculateScrollAmount(contentWindow);

        if (platform.isWindows) {
            contentWindow.scrollTo(contentWindow.scrollX - scrollAmount, 0);
        } else {
            contentWindow.scrollTo({
                left: contentWindow.scrollX - scrollAmount,
                top: 0,
                behavior: "smooth"
            });
        }
        if (currentPage > 0) {
            currentPage--;
        } else {
            console.warn("Already at the first page, cannot scroll left.");
        }
        console.log("Scrolled left to page:", currentPage);
        updateCharacterPosition();
    },

    /**
     * Scrolls the content right by one page
     * @param {Object} platform - Platform flags
     */
    scrollRight(platform) {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) return;

        const scrollAmount = this.calculateScrollAmount(contentWindow);

        if (platform.isWindows) {
            contentWindow.scrollTo(contentWindow.scrollX + scrollAmount, 0);
        } else {
            contentWindow.scrollTo({
                left: contentWindow.scrollX + scrollAmount,
                top: 0,
                behavior: "smooth"
            });
        }
        currentPage++;
        console.log("Scrolled right to page:", currentPage);
        updateCharacterPosition();
    },

    /**
     * Scrolls to a specific page in the iframe content
     * @param {number} page - The page number to scroll to (0-based index)
     * @returns {void}
     */
    scrollToPage(page) {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) return;
        for (let i = 0; i < page; i++) {
            if (this.isHorizontallyScrolledToEnd()) {
                console.warn("Already at the last page, cannot scroll further.");
                return;
            }
            const scrollAmount = this.calculateScrollAmount(contentWindow);
            contentWindow.scrollTo(contentWindow.scrollX + scrollAmount, 0);
        }
    },

    /**
     * Calculates the scroll amount for one page
     * @param {Window} contentWindow - The iframe content window
     * @returns {number} The scroll amount in pixels
     */
    calculateScrollAmount(contentWindow) {
        const gap = parseInt(
            window.getComputedStyle(contentWindow.document.documentElement)
                .getPropertyValue("column-gap")
        ) || 0;

        return contentWindow.innerWidth + gap;
    },

    /**
     * Scrolls to the horizontal end of content
     */
    scrollToHorizontalEnd() {
        if (!frame) {
            console.error("Iframe element not found for scrollToHorizontalEnd.");
            return;
        }

        // Check if contentWindow and document are ready
        if (frame.contentWindow && frame.contentWindow.document.readyState === 'complete') {
            const contentDoc = frame.contentDocument || frame.contentWindow.document;
            const maxScrollLeft = contentDoc.documentElement.scrollWidth - contentDoc.documentElement.clientWidth;
            console.log("Scrolling to end of container.");
            frame.contentWindow.scrollTo(maxScrollLeft, 0);
        } else {
            // If iframe not ready, set onload to call this function again
            frame.onload = function () {
                navigationUtils.scrollToHorizontalEnd();
                frame.onload = null; // Remove listener to prevent multiple calls
            };
        }
    }
};

/**
 * Security utilities
 */
const securityUtils = {
    /**
     * Validates the origin of incoming messages
     * @param {string} origin - The origin to validate
     * @param {Object} platform - Platform flags
     * @returns {boolean} True if origin is allowed
     */
    validateOrigin(origin, platform) {
        const isAllowedOrigin =
            ((platform.isAndroid || platform.isWindows) && origin === "https://demo") ||
            ((platform.isIOS || platform.isMac) && origin === "app://demo");

        if (!isAllowedOrigin) {
            console.warn("Received message from unauthorized origin:", origin);
        }

        return isAllowedOrigin;
    }
};

/**
 * Layout and dimension utilities
 */
const layoutUtils = {
    /**
     * Sets dimensions for the iframe and body elements
     * @param {HTMLElement} body - The body element
     * @param {HTMLElement} root - The document root element
     */
    setDimensions(body, root) {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) {
            console.warn("Iframe contentWindow is not available for dimension setting yet.");
            return;
        }

        const width = Math.floor(contentWindow.innerWidth);
        const height = Math.floor(contentWindow.innerHeight);

        frame.style.width = `${width}px`;
        body.style.width = `${width}px`;
        body.style.height = `${height}px`;
        frame.style.height = `${height}px`;

        root.style.setProperty('--root-width', `${width}px`);
        root.style.setProperty('--root-height', `${height}px`);
    },
};

/**
 * Column management utilities
 */
const columnUtils = {
    /**
     * Gets the number of columns per screen based on CSS column properties
     * @param {Window} contentWindow - The iframe content window
     * @returns {number|null} The number of columns per screen, or null if in scroll mode
     */
    getColumnCountPerScreen(contentWindow) {
        if (!contentWindow?.document) {
            console.warn("Content window not available for column count calculation.");
            return null;
        }

        const doc = contentWindow.document;
        const computedStyle = contentWindow.getComputedStyle(doc.documentElement);

        // Check if we're in column mode by looking for column-count or column-width
        const columnCount = computedStyle.getPropertyValue('column-count');
        const columnWidth = computedStyle.getPropertyValue('column-width');

        if (columnCount && columnCount !== 'auto') {
            return parseInt(columnCount, 10);
        }

        if (columnWidth && columnWidth !== 'auto') {
            const width = parseFloat(columnWidth);
            const containerWidth = contentWindow.innerWidth;
            const gap = parseFloat(computedStyle.getPropertyValue('column-gap')) || 0;
            return Math.floor((containerWidth + gap) / (width + gap));
        }

        // Default to 1 column if no column properties are set
        return 1;
    },

    /**
     * Gets the content height of the document element
     * @param {HTMLElement} documentElement - The document element
     * @returns {number} The content height in pixels
     */
    getContentHeight(documentElement) {
        if (!documentElement) {
            console.warn("Document element not available for height calculation.");
            return 0;
        }

        // Get the computed style height or use scrollHeight as fallback
        const computedStyle = window.getComputedStyle(documentElement);
        const height = parseFloat(computedStyle.height);

        return isNaN(height) ? documentElement.scrollHeight : height;
    },

    /**
     * We have to make sure that the total number of columns is a multiple 
     * of the number of columns per screen. 
     * Otherwise it causes snapping and page turning issues. 
     * To fix this, we insert and remove blank virtual columns at the end of the resource.
     * @param {Window} contentWindow - The iframe content window
     * @returns {boolean} True if virtual columns were added or removed
     */
    appendVirtualColumnIfNeeded(contentWindow) {
        if (!contentWindow?.document) {
            console.warn("Content window not available for virtual column management.");
            return false;
        }

        const colCountPerScreen = this.getColumnCountPerScreen(contentWindow);
        console.log(`Column count per screen: ${colCountPerScreen}`);

        if (!colCountPerScreen) {
            // This has been triggered while in scroll mode
            console.log("In scroll mode, skipping virtual column adjustment.");
            return false;
        }

        const doc = contentWindow.document;
        const virtualCols = doc.querySelectorAll("div[id^='readium-virtual-page']");

        // Remove first so that we don't end up with an incorrect scrollWidth
        // Even when removing their width we risk having an incorrect scrollWidth
        // so removing them entirely is the most robust solution
        for (const virtualCol of virtualCols) {
            virtualCol.remove();
        }
        const virtualColsCount = virtualCols.length;
        console.log(`Found ${virtualColsCount} existing virtual columns.`);

        const documentWidth = doc.scrollingElement ? doc.scrollingElement.scrollWidth : doc.documentElement.scrollWidth;
        const windowWidth = contentWindow.visualViewport ? contentWindow.visualViewport.width : contentWindow.innerWidth;

        console.log(`Document width: ${documentWidth}, Window width: ${windowWidth}`);

        const totalColCount = Math.round((documentWidth / windowWidth) * colCountPerScreen);
        const lonelyColCount = totalColCount % colCountPerScreen;

        const needed = colCountPerScreen === 1 || lonelyColCount === 0
            ? 0
            : colCountPerScreen - lonelyColCount;

        console.log(`Virtual columns - Total: ${totalColCount}, Per screen: ${colCountPerScreen}, Lonely: ${lonelyColCount}, Needed: ${needed}`);

        if (needed > 0) {
            for (let i = 0; i < needed; i++) {
                const virtualCol = doc.createElement("div");
                virtualCol.setAttribute("id", `readium-virtual-page-${i}`);
                virtualCol.dataset.readium = "true";

                // Check for CSS column break support
                if (CSS?.supports("break-before", "column")) {
                    virtualCol.style.breakBefore = "column";
                } else if (CSS?.supports("break-inside", "avoid-column")) {
                    virtualCol.style.breakInside = "avoid-column";
                    virtualCol.style.height = this.getContentHeight(doc.documentElement) + "px";
                } else {
                    virtualCol.style.height = this.getContentHeight(doc.documentElement) + "px";
                }

                virtualCol.innerHTML = "&#8203;"; // zero-width space
                doc.body.appendChild(virtualCol);
            }

            console.log(`Added ${needed} virtual columns to fix column alignment.`);
        }
        else {
            console.log("No virtual columns needed, document is already aligned.");
        }

        return virtualColsCount !== needed;
    }
};

/**
 * Applies virtual column adjustment to fix column alignment issues
 * This should be called after content is loaded or when layout changes
 * @returns {boolean} True if virtual columns were adjusted
 */
function adjustVirtualColumns() {
    const contentWindow = domUtils.getContentWindow();
    if (!contentWindow) {
        console.warn("Cannot adjust virtual columns - iframe content not available.");
        return false;
    }

    try {
        return columnUtils.appendVirtualColumnIfNeeded(contentWindow);
    } catch (error) {
        console.error("Error adjusting virtual columns:", error);
        return false;
    }
}

/**
 * Style and property utilities
 */
const styleUtils = {
    /**
     * Sets a CSS custom property on the iframe document
     * @param {string} property - The CSS property name
     * @param {string} value - The CSS property value
     */
    setReadiumProperty(property, value) {
        const root = domUtils.getIframeDocument()?.documentElement;
        if (!root) {
            console.warn(`Could not set property '${property}'. Iframe content not accessible or not loaded.`);
            return;
        }

        console.log(`Setting iframe CSS property: ${property} = ${value}`);
        root.style.setProperty(property, value);

        // Adjust virtual columns after column-related property changes
        if (property.includes('column')) {
            setTimeout(() => {
                adjustVirtualColumns();
            }, 50);
        }
    },

    /**
     * Removes a CSS custom property from the iframe document
     * @param {string} property - The CSS property name
     */
    unsetReadiumProperty(property) {
        const root = domUtils.getIframeDocument()?.documentElement;
        if (!root) {
            console.warn(`Could not unset property '${property}'. Iframe content not accessible or not loaded.`);
            return;
        }

        console.log(`Unsetting iframe CSS property: ${property}`);
        root.style.removeProperty(property);
    },

    /**
     * Sets the background color
     * @param {string} color - The color value
     */
    setBackgroundColor(color) {
        if (color == null || color === '') {
            console.log("No color provided, unsetting background color.");
            document.documentElement.style.removeProperty('--background-color');
            return;
        }

        console.log(`Setting background color to: ${color}`);
        document.documentElement.style.setProperty('--background-color', color);
    },

    /**
     * Removes the background color
     */
    unsetBackgroundColor() {
        document.documentElement.style.removeProperty('--background-color');
    }
};

/**
 * Event handling for messages
 * @param {MessageEvent} event - The message event
 * @param {Object} platform - Platform flags
 */
function handleMessage(event, platform) {
    // Origin validation for security
    if (!securityUtils.validateOrigin(event.origin, platform)) {
        return;
    }

    const { data } = event;

    if (data.startsWith("jump.")) {
        const href = data.substring(5);
        console.log("Jumping to:", href);
        window.location.href = `https://runcsharp.jump?${href}`;
    } else if (data === "next") {
        handleNextCommand();
    } else if (data === "prev") {
        handlePrevCommand();
    } else if (data === "menu") {
        console.log("Received menu command.");
        window.location.href = 'https://runcsharp.menu?true';
    }
}

/**
 * Handles the "next" command
 */
function handleNextCommand() {
    let platform = domUtils.detectPlatform();
    if (navigationUtils.isHorizontallyScrolledToEnd()) {
        console.log("Reached end of current content, requesting next page.");
        window.location.href = 'https://runcsharp.next?true';
    } else {
        navigationUtils.scrollRight(platform);
    }
}

/**
 * Handles the "prev" command
 */
function handlePrevCommand() {
    let platform = domUtils.detectPlatform();
    if (navigationUtils.isHorizontalScrollAtStart()) {
        console.log("Reached start of current content, requesting previous page.");
        window.location.href = 'https://runcsharp.prev?true';
    } else {
        navigationUtils.scrollLeft(platform);
    }
}


/**
 * Calculates the approximate character position based on current scroll position
 * This is used to determine which synthetic page is currently being displayed
 * @returns {number} The estimated character position in the current document
 */
function getCharacterPositionFromScroll() {
    const contentWindow = domUtils.getContentWindow();
    const doc = domUtils.getIframeDocument();

    if (!contentWindow || !doc) {
        console.warn("Cannot calculate character position - iframe content not accessible");
        return 0;
    }

    try {
        // Get the current scroll position and total scrollable width
        const currentScrollX = contentWindow.scrollX;
        const totalScrollWidth = doc.documentElement.scrollWidth;
        const viewportWidth = contentWindow.innerWidth;

        // Calculate the scroll progress as a percentage (0-1)
        const maxScrollX = Math.max(0, totalScrollWidth - viewportWidth);
        const scrollProgress = maxScrollX > 0 ? Math.min(1, currentScrollX / maxScrollX) : 0;

        // Get the total text content from the document
        const textContent = extractTextFromDocument(doc);
        const totalCharacters = textContent.length;

        // Calculate character position based on scroll progress
        const characterPosition = Math.floor(scrollProgress * totalCharacters);

        console.log(`Character position calculated: ${characterPosition} (scroll: ${scrollProgress.toFixed(3)}, total chars: ${totalCharacters})`);

        return Math.max(0, characterPosition);
    } catch (error) {
        console.error("Error calculating character position:", error);
        return 0;
    }
}

/**
 * Extracts text content from the document, similar to the C# ExtractTextFromHtml method
 * @param {Document} doc - The document to extract text from
 * @returns {string} The extracted text content
 */
function extractTextFromDocument(doc) {
    if (!doc?.body) {
        return "";
    }

    try {
        // Get the text content from the body, which automatically excludes HTML tags
        let textContent = doc.body.textContent || doc.body.innerText || "";

        // Normalize whitespace while preserving paragraph breaks
        textContent = textContent.replace(/\s+/g, " ").trim();

        return textContent;
    } catch (error) {
        console.error("Error extracting text from document:", error);
        return "";
    }
}

/**
 * Gets the character position for the current page and notifies the C# code
 * This is called when pages change to update synthetic page information
 */
function updateCharacterPosition() {
    const characterPosition = getCharacterPositionFromScroll();
    console.log(`Updating character position: ${characterPosition}`);
    // Notify C# code about the character position change
    window.location.href = `https://runcsharp.characterposition?${characterPosition}`;
}


/**
 * Initialize the reader when the DOM is fully loaded
 */
document.addEventListener("DOMContentLoaded", function () {
    console.log('Document ready');
    frame = document.getElementById("page");
    const body = document.getElementById("body");
    const root = document.documentElement;
    const platform = domUtils.detectPlatform();

    if (!frame || !body) {
        console.error("Required DOM elements (iframe with id 'page' or body with id 'body') not found.");
        return;
    }

    // Add platform-specific class to body
    if (platform.isWindows) {
        body.classList.add('windows-platform');
        frame.classList.add('windows-platform');
    }

    // Set initial dimensions and add resize listener
    layoutUtils.setDimensions(body, root);
    frame.contentWindow?.addEventListener('resize', () => layoutUtils.setDimensions(body, root));

    // Handle iframe load events
    frame.onload = function () {
        try {
            const contentWindow = domUtils.getContentWindow();
            if (!contentWindow?.document) {
                console.error("Cannot access iframe content - likely CORS restriction or iframe not fully loaded.");
                return;
            }
            if (!isPreviousPage) {
                // Adjust virtual columns after content loads
                setTimeout(() => {
                    adjustVirtualColumns();
                }, 100); // Small delay to ensure content is fully rendered
            }

            window.location.href = 'https://runcsharp.pageLoad?true';
        } catch (error) {
            console.error("Error during iframe onload:", error);
        }
    };

    // Listen for messages from the parent window
    window.addEventListener("message", event => handleMessage(event, platform));
    if (platform.isIOS) {
        window.addEventListener('touchstart', {});
    }
});

// Public API functions

/**
 * Retrieves the width of the content in the iframe
 * @returns {number} The inner width of the content window
 */
function getWidth() {
    const contentWindow = domUtils.getContentWindow();
    if (!contentWindow) {
        console.warn("Iframe contentWindow not available in getWidth.");
        return 0;
    }
    return Math.floor(contentWindow.innerWidth);
}

/**
 * Calculates the number of pages within the iframe
 * @returns {number} The calculated number of pages
 */
function getPageCount() {
    const contentWindow = domUtils.getContentWindow();
    if (!contentWindow) {
        console.warn("Iframe contentWindow not available in getPageCount.");
        return 0;
    }

    const width = Math.ceil(contentWindow.innerWidth);
    const containerWidth = Math.ceil(contentWindow.document.documentElement.scrollWidth);
    return Math.ceil(Math.max(1, containerWidth / width)) - 1;
}

/**
 * Retrieves the current page number
 * @returns {number} The current page number
 */
function getCurrentPage() {
    return currentPage;
}

/**
 * Navigates to the end of content if previous page flag is set
 */
function gotoEnd() {
    if (isPreviousPage) {
        adjustVirtualColumns();

        // Adjust virtual columns after positioning content at the end
        // This ensures virtual columns are calculated with the correct final position
        setTimeout(() => {
            navigationUtils.scrollToHorizontalEnd();
            isPreviousPage = false;
            currentPage = getPageCount();
            updateCharacterPosition();
        }, 200);
    }
}

/**
 * Sets a flag indicating that navigation to previous page occurred
 */
function setPreviousPage() {
    isPreviousPage = true;
    currentPage = 0; // Reset current page on previous navigation
}

/**
 * Loads a specified URL into the iframe
 * @param {string} page - The URL to load
 * @returns {boolean} True if the page was loaded
 */
function loadPage(page) {
    if (!frame) {
        console.error("Frame not found for loadPage.");
        return false;
    }
    console.log("Frame found. Loading page:", page);
    frame.setAttribute('src', page);
    currentPage = 0; // Reset current page on new load
    return true;
}

/**
 * Goes to a specific page in the iframe content
 * @param {number} page property - The page number to navigate to
 * @returns {void}
 */
function gotoPage(page) {
    console.log("Jumping to page:", page);
    if (page < 1) {
        console.warn("Page number must be 1 or greater. Current page:", page);
        currentPage = 0;
        return;
    }
    //navigationUtils.scrollToPage(page);
    adjustVirtualColumns();


    // Adjust virtual columns after positioning content at the end
    // This ensures virtual columns are calculated with the correct final position
    setTimeout(() => {
        navigationUtils.scrollToPage(page);
        isPreviousPage = false;
        currentPage = getPageCount();
        updateCharacterPosition();
    }, 200);
}

/**
 * Scrolls the iframe content to its horizontal end
 */
function scrollToHorizontalEnd() {
    navigationUtils.scrollToHorizontalEnd();
}

/**
 * Sets a CSS custom property on the iframe's document element
 * @param {string} property - The CSS property name
 * @param {string} value - The CSS property value
 */
function setReadiumProperty(property, value) {
    styleUtils.setReadiumProperty(property, value);
}

/**
 * Removes a CSS custom property from the iframe's document element
 * @param {string} property - The CSS property name
 */
function unsetReadiumProperty(property) {
    styleUtils.unsetReadiumProperty(property);
}

/**
 * Sets the background color CSS custom property
 * @param {string} color - The color value
 */
function setBackgroundColor(color) {
    styleUtils.setBackgroundColor(color);
}

/**
 * Removes the background color CSS custom property
 */
function unsetBackgroundColor() {
    styleUtils.unsetBackgroundColor();
}