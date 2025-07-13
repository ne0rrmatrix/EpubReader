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
        updateCharacterPosition();
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
            currentPage = getPageCount();
            updateCharacterPosition();
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
    return Math.ceil(Math.max(1, containerWidth / width));
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
        navigationUtils.scrollToHorizontalEnd();
        isPreviousPage = false;
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
        return;
    }
    navigationUtils.scrollToPage(page);
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