let isPreviousPage = false;
let frame = null;
let colCount = 1;

/**
 * Detects the user's operating system platform.
 * @returns {object} An object indicating the detected platform.
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

document.addEventListener("DOMContentLoaded", function () {
    frame = document.getElementById("page");
    const body = document.getElementById("body");
    const root = document.documentElement;
    const platform = detectPlatform();

    if (!frame || !body) {
        console.error("Required DOM elements (iframe with id 'page' or body with id 'body') not found.");
        return; // Exit if essential elements are missing
    }

    /**
     * Sets the dimensions of the iframe and body, and updates CSS custom properties.
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

    frame.onload = function () {
        try {
            if (!frame.contentWindow?.document) {
                console.error("Cannot access iframe content - likely CORS restriction or iframe not fully loaded.");
                return;
            }

            // Early exit for mobile platforms as per original logic
            if (platform.isAndroid || platform.isIOS) {
                console.log("Not setting extra page for single column on mobile.");
                return;
            }

            /**
             * Calculates the number of pages within the iframe's content based on scroll width and inner width.
             * @returns {number} The calculated page count.
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
     * Checks if the horizontal scroll position within the iframe is at the start.
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
     * Checks if the horizontal scroll position within the iframe is at the end.
     * Allows for a small tolerance.
     * @returns {boolean} True if at the end, false otherwise.
     */
    const isHorizontallyScrolledToEnd = () => {
        if (!frame.contentWindow) {
            return false;
        }
        const contentDoc = frame.contentWindow.document.documentElement;
        const maxScrollLeft = contentDoc.scrollWidth - contentDoc.clientWidth;
        // Allow a small tolerance for floating point inaccuracies
        return Math.abs(frame.contentWindow.scrollX - maxScrollLeft) <= 30;
    };

    /**
     * Scrolls the iframe content to the left by one page.
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
     * Scrolls the iframe content to the right by one page.
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
 * Retrieves the calculated page count within the iframe.
 * NOTE: This function re-queries the DOM for the iframe and its contentWindow,
 * which might be less efficient if called frequently outside the `DOMContentLoaded` scope.
 * Consider passing `frame` as an argument if frequent external calls are expected.
 * @returns {number} The number of pages.
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
 * Scrolls the iframe content to its horizontal end if 'isPreviousPage' flag is true, then resets the flag.
 * This function depends on `scrollToHorizontalEnd`.
 */
function gotoEnd() {
    if (isPreviousPage) {
        scrollToHorizontalEnd();
        isPreviousPage = false;
    }
}

/**
 * Sets a flag indicating that the previous page was navigated to.
 */
function setPreviousPage() {
    isPreviousPage = true;
}

/**
 * Loads a specified URL into the iframe.
 * @param {string} page - The URL to load.
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
 * Scrolls the iframe content to its horizontal end.
 * Handles cases where the iframe content might not be fully loaded yet by retrying on `onload`.
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
    document.documentElement.style.setProperty('--background-color', color);
}

/**
 * Removes the background color CSS custom property from the main document element.
 */
function unsetBackgroundColor() {
    document.documentElement.style.removeProperty('--background-color');
}