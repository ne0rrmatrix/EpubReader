/**
 * EPUB Reader JavaScript Interface
 * Handles rendering, navigation, and styling of EPUB content
 */

// Global state
let isPreviousPage = false;
let currentPage = 0;
let frame = null;
let colCount = 1;

// Constants
const CONSTANTS = {
    SCROLL_TOLERANCE: 30,
    VIRTUAL_COLUMN_DELAY: 50,
    ZERO_WIDTH_SPACE: "&#8203;",
    VIRTUAL_COLUMN_PREFIX: "readium-virtual-page",
    URLS: {
        JUMP: "https://runcsharp.jump",
        NEXT: "https://runcsharp.next",
        PREV: "https://runcsharp.prev",
        MENU: "https://runcsharp.menu",
        PAGE_LOAD: "https://runcsharp.pageLoad",
        CHARACTER_POSITION: "https://runcsharp.characterposition"
    },
    ORIGINS: {
        ANDROID_WINDOWS: "https://demo",
        IOS_MAC: "app://demo"
    }
};

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
        const isMacDevice = /macintosh|mac os x/.test(userAgent);
        const isMobileDevice = /iphone|ipad|ipod/.test(userAgent);
        const hasTouchPoints = navigator.maxTouchPoints > 1;

        return {
            isIOS: isMobileDevice || (isMacDevice && hasTouchPoints),
            isMac: isMacDevice && !isMobileDevice && !hasTouchPoints,
            isWindows: /win32|win64|windows|wince/.test(userAgent),
            isAndroid: /android/.test(userAgent)
        };
    },

    /**
     * Checks if iframe content is ready and accessible
     * @returns {boolean} True if iframe is ready
     */
    isIframeReady() {
        return frame?.contentWindow?.document?.readyState === 'complete';
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
        return Math.abs(contentWindow.scrollX - maxScrollLeft) <= CONSTANTS.SCROLL_TOLERANCE;
    },

    /**
     * Performs smooth or instant scroll based on platform
     * @param {Window} contentWindow - The content window
     * @param {number} scrollX - Target scroll position
     * @param {boolean} isWindows - Whether platform is Windows
     */
    performScroll(contentWindow, scrollX, platform) {
        if (platform.isWindows) {
            contentWindow.scrollTo(scrollX, 0);
        } else {
            contentWindow.scrollTo({
                left: scrollX,
                top: 0,
                behavior: "smooth"
            });
        }
    },

    /**
     * Scrolls the content left by one page
     * @param {Object} platform - Platform flags
     */
    scrollLeft(platform) {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) return;

        const scrollAmount = this.calculateScrollAmount(contentWindow);
        const newScrollX = contentWindow.scrollX - scrollAmount;
        
        this.performScroll(contentWindow, newScrollX, platform);
        
        if (currentPage > 0) {
            currentPage--;
            console.log("Scrolled left to page:", currentPage);
        } else {
            console.warn("Already at the first page, cannot scroll left.");
        }
        
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
        const newScrollX = contentWindow.scrollX + scrollAmount;
        
        this.performScroll(contentWindow, newScrollX, platform);
        
        currentPage++;
        console.log("Scrolled right to page:", currentPage);
        updateCharacterPosition();
    },

    /**
     * Scrolls to a specific page in the iframe content
     * @param {number} page - The page number to scroll to (0-based index)
     */
    scrollToPage(page) {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) return;
        
        const scrollAmount = this.calculateScrollAmount(contentWindow);
        
        for (let i = 0; i < page; i++) {
            if (this.isHorizontallyScrolledToEnd()) {
                console.warn("Already at the last page, cannot scroll further.");
                return;
            }
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

        if (domUtils.isIframeReady()) {
            const contentDoc = frame.contentDocument || frame.contentWindow.document;
            const maxScrollLeft = contentDoc.documentElement.scrollWidth - contentDoc.documentElement.clientWidth;
            console.log("Scrolling to end of container.");
            frame.contentWindow.scrollTo(maxScrollLeft, 0);
        } else {
            frame.onload = () => {
                this.scrollToHorizontalEnd();
                frame.onload = null;
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
            ((platform.isAndroid || platform.isWindows) && origin === CONSTANTS.ORIGINS.ANDROID_WINDOWS) ||
            ((platform.isIOS || platform.isMac) && origin === CONSTANTS.ORIGINS.IOS_MAC);

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

        const { width, height } = this.calculateDimensions(contentWindow);
        this.applyDimensions(body, root, width, height);
    },

    /**
     * Calculates dimensions from content window
     * @param {Window} contentWindow - The iframe content window
     * @returns {Object} Width and height values
     */
    calculateDimensions(contentWindow) {
        return {
            width: Math.floor(contentWindow.innerWidth),
            height: Math.floor(contentWindow.innerHeight)
        };
    },

    /**
     * Applies dimensions to elements
     * @param {HTMLElement} body - The body element
     * @param {HTMLElement} root - The document root element
     * @param {number} width - Width value
     * @param {number} height - Height value
     */
    applyDimensions(body, root, width, height) {
        const widthPx = `${width}px`;
        const heightPx = `${height}px`;

        frame.style.width = widthPx;
        frame.style.height = heightPx;
        body.style.width = widthPx;
        body.style.height = heightPx;

        root.style.setProperty('--root-width', widthPx);
        root.style.setProperty('--root-height', heightPx);
    }
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

        const computedStyle = contentWindow.getComputedStyle(contentWindow.document.documentElement);
        const columnCount = computedStyle.getPropertyValue('column-count');
        const columnWidth = computedStyle.getPropertyValue('column-width');

        if (columnCount && columnCount !== 'auto') {
            return parseInt(columnCount, 10);
        }

        if (columnWidth && columnWidth !== 'auto') {
            return this.calculateColumnsFromWidth(columnWidth, contentWindow, computedStyle);
        }

        return 1;
    },

    /**
     * Calculates column count from column width
     * @param {string} columnWidth - CSS column width value
     * @param {Window} contentWindow - The iframe content window
     * @param {CSSStyleDeclaration} computedStyle - Computed style object
     * @returns {number} Number of columns
     */
    calculateColumnsFromWidth(columnWidth, contentWindow, computedStyle) {
        const width = parseFloat(columnWidth);
        const containerWidth = contentWindow.innerWidth;
        const gap = parseFloat(computedStyle.getPropertyValue('column-gap')) || 0;
        return Math.floor((containerWidth + gap) / (width + gap));
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

        const computedStyle = window.getComputedStyle(documentElement);
        const height = parseFloat(computedStyle.height);
        return isNaN(height) ? documentElement.scrollHeight : height;
    },

    /**
     * Creates a virtual column element
     * @param {Document} doc - The document
     * @param {number} index - Column index
     * @returns {HTMLElement} The virtual column element
     */
    createVirtualColumn(doc, index) {
        const virtualCol = doc.createElement("div");
        virtualCol.setAttribute("id", `${CONSTANTS.VIRTUAL_COLUMN_PREFIX}-${index}`);
        virtualCol.dataset.readium = "true";
        virtualCol.innerHTML = CONSTANTS.ZERO_WIDTH_SPACE;

        // Apply appropriate CSS break styles
        if (CSS?.supports("break-before", "column")) {
            virtualCol.style.breakBefore = "column";
        } else {
            const height = this.getContentHeight(doc.documentElement);
            virtualCol.style.height = `${height}px`;
            
            if (CSS?.supports("break-inside", "avoid-column")) {
                virtualCol.style.breakInside = "avoid-column";
            }
        }

        return virtualCol;
    },

    /**
     * Manages virtual columns to ensure proper alignment
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
            console.log("In scroll mode, skipping virtual column adjustment.");
            return false;
        }

        const doc = contentWindow.document;
        const virtualCols = doc.querySelectorAll(`div[id^='${CONSTANTS.VIRTUAL_COLUMN_PREFIX}']`);
        const virtualColsCount = virtualCols.length;

        // Remove existing virtual columns
        virtualCols.forEach(col => col.remove());
        console.log(`Found ${virtualColsCount} existing virtual columns.`);

        const { needed } = this.calculateVirtualColumnNeeds(contentWindow, colCountPerScreen);
        console.log(`Virtual columns needed: ${needed}`);

        if (needed > 0) {
            this.addVirtualColumns(doc, needed);
            console.log(`Added ${needed} virtual columns to fix column alignment.`);
        } else {
            console.log("No virtual columns needed, document is already aligned.");
        }

        return virtualColsCount !== needed;
    },

    /**
     * Calculates how many virtual columns are needed
     * @param {Window} contentWindow - The iframe content window
     * @param {number} colCountPerScreen - Columns per screen
     * @returns {Object} Calculation results
     */
    calculateVirtualColumnNeeds(contentWindow, colCountPerScreen) {
        const doc = contentWindow.document;
        const documentWidth = doc.scrollingElement?.scrollWidth || doc.documentElement.scrollWidth;
        const windowWidth = contentWindow.visualViewport?.width || contentWindow.innerWidth;

        console.log(`Document width: ${documentWidth}, Window width: ${windowWidth}`);

        const totalColCount = Math.round((documentWidth / windowWidth) * colCountPerScreen);
        const lonelyColCount = totalColCount % colCountPerScreen;
        const needed = (colCountPerScreen === 1 || lonelyColCount === 0) ? 0 : colCountPerScreen - lonelyColCount;

        console.log(`Virtual columns - Total: ${totalColCount}, Per screen: ${colCountPerScreen}, Lonely: ${lonelyColCount}, Needed: ${needed}`);

        return { needed, totalColCount, lonelyColCount };
    },

    /**
     * Adds virtual columns to the document
     * @param {Document} doc - The document
     * @param {number} count - Number of columns to add
     */
    addVirtualColumns(doc, count) {
        for (let i = 0; i < count; i++) {
            const virtualCol = this.createVirtualColumn(doc, i);
            doc.body.appendChild(virtualCol);
        }
    }
};

/**
 * Style and property utilities
 */
const styleUtils = {
    /**
     * Gets the document root element
     * @returns {HTMLElement|null} The document root element
     */
    getRoot() {
        return domUtils.getIframeDocument()?.documentElement || null;
    },

    /**
     * Sets a CSS custom property on the iframe document
     * @param {string} property - The CSS property name
     * @param {string} value - The CSS property value
     */
    setReadiumProperty(property, value) {
        const root = this.getRoot();
        if (!root) {
            console.warn(`Could not set property '${property}'. Iframe content not accessible or not loaded.`);
            return;
        }

        console.log(`Setting iframe CSS property: ${property} = ${value}`);
        
        if (property === '--USER__colCount') {
            colCount = parseInt(value, 10);
            console.log(`Updated colCount to: ${colCount}`);
        }
        
        root.style.setProperty(property, value);
    },

    /**
     * Removes a CSS custom property from the iframe document
     * @param {string} property - The CSS property name
     */
    unsetReadiumProperty(property) {
        const root = this.getRoot();
        if (!root) {
            console.warn(`Could not unset property '${property}'. Iframe content not accessible or not loaded.`);
            return;
        }

        console.log(`Unsetting iframe CSS property: ${property}`);
        root.style.removeProperty(property);
    },

    /**
     * Manages background color CSS property
     * @param {string|null} color - The color value or null to remove
     */
    manageBackgroundColor(color) {
        const root = document.documentElement;
        const property = '--background-color';

        if (!color) {
            console.log("No color provided, unsetting background color.");
            root.style.removeProperty(property);
            return;
        }

        console.log(`Setting background color to: ${color}`);
        root.style.setProperty(property, color);
    }
};

/**
 * Character position utilities
 */
const characterUtils = {
    /**
     * Calculates the approximate character position based on current scroll position
     * @returns {number} The estimated character position in the current document
     */
    getCharacterPositionFromScroll() {
        const contentWindow = domUtils.getContentWindow();
        const doc = domUtils.getIframeDocument();
        
        if (!contentWindow || !doc) {
            console.warn("Cannot calculate character position - iframe content not accessible");
            return 0;
        }

        try {
            const scrollData = this.getScrollData(contentWindow, doc);
            const textContent = this.extractTextFromDocument(doc);
            const characterPosition = Math.floor(scrollData.scrollProgress * textContent.length);
            
            console.log(`Character position calculated: ${characterPosition} (scroll: ${scrollData.scrollProgress.toFixed(3)}, total chars: ${textContent.length})`);
            
            return Math.max(0, characterPosition);
        } catch (error) {
            console.error("Error calculating character position:", error);
            return 0;
        }
    },

    /**
     * Gets scroll-related data for character position calculation
     * @param {Window} contentWindow - The iframe content window
     * @param {Document} doc - The iframe document
     * @returns {Object} Scroll data object
     */
    getScrollData(contentWindow, doc) {
        const currentScrollX = contentWindow.scrollX;
        const totalScrollWidth = doc.documentElement.scrollWidth;
        const viewportWidth = contentWindow.innerWidth;
        const maxScrollX = Math.max(0, totalScrollWidth - viewportWidth);
        const scrollProgress = maxScrollX > 0 ? Math.min(1, currentScrollX / maxScrollX) : 0;

        return { currentScrollX, totalScrollWidth, viewportWidth, maxScrollX, scrollProgress };
    },

    /**
     * Extracts text content from the document
     * @param {Document} doc - The document to extract text from
     * @returns {string} The extracted text content
     */
    extractTextFromDocument(doc) {
        if (!doc?.body) return "";
        
        try {
            let textContent = doc.body.textContent || doc.body.innerText || "";
            return textContent.replace(/\s+/g, " ").trim();
        } catch (error) {
            console.error("Error extracting text from document:", error);
            return "";
        }
    }
};

/**
 * Navigation command handlers
 */
const commandHandlers = {
    /**
     * Handles navigation commands with platform detection
     * @param {string} command - The command to handle
     */
    handleNavigationCommand(command) {
        const platform = domUtils.detectPlatform();
        
        if (command === "next") {
            this.handleNext(platform);
        } else if (command === "prev") {
            this.handlePrev(platform);
        }
    },

    /**
     * Handles the "next" command
     * @param {Object} platform - Platform flags
     */
    handleNext(platform) {
        if (navigationUtils.isHorizontallyScrolledToEnd()) {
            console.log("Reached end of current content, requesting next page.");
            window.location.href = `${CONSTANTS.URLS.NEXT}?true`;
        } else {
            navigationUtils.scrollRight(platform);
        }
    },

    /**
     * Handles the "prev" command
     * @param {Object} platform - Platform flags
     */
    handlePrev(platform) {
        if (navigationUtils.isHorizontalScrollAtStart()) {
            console.log("Reached start of current content, requesting previous page.");
            window.location.href = `${CONSTANTS.URLS.PREV}?true`;
        } else {
            navigationUtils.scrollLeft(platform);
        }
    }
};

/**
 * Main application utilities
 */
const appUtils = {
    /**
     * Adjusts virtual columns for proper alignment
     * @returns {boolean} True if virtual columns were adjusted
     */
    adjustVirtualColumns() {
        if (colCount === 1) {
            console.log("Single column mode detected, skipping virtual column adjustment.");
            return false;
        }

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
    },

    /**
     * Updates character position and notifies C# code
     */
    updateCharacterPosition() {
        const characterPosition = characterUtils.getCharacterPositionFromScroll();
        console.log(`Updating character position: ${characterPosition}`);
        window.location.href = `${CONSTANTS.URLS.CHARACTER_POSITION}?${characterPosition}`;
    },

    /**
     * Handles incoming messages from parent window
     * @param {MessageEvent} event - The message event
     * @param {Object} platform - Platform flags
     */
    handleMessage(event, platform) {
        if (!securityUtils.validateOrigin(event.origin, platform)) {
            return;
        }

        const { data } = event;

        if (data.startsWith("jump.")) {
            const href = data.substring(5);
            console.log("Jumping to:", href);
            window.location.href = `${CONSTANTS.URLS.JUMP}?${href}`;
        } else if (data === "next" || data === "prev") {
            commandHandlers.handleNavigationCommand(data);
        } else if (data === "menu") {
            console.log("Received menu command.");
            window.location.href = `${CONSTANTS.URLS.MENU}?true`;
        }
    },

    /**
     * Performs navigation to end with optional delay
     * @param {boolean} useDelay - Whether to use delay for multi-column mode
     */
    performNavigationToEnd(useDelay = false) {
        const action = () => {
            navigationUtils.scrollToHorizontalEnd();
            isPreviousPage = false;
            currentPage = getPageCount();
            this.updateCharacterPosition();
        };

        if (useDelay) {
            setTimeout(action, CONSTANTS.VIRTUAL_COLUMN_DELAY);
        } else {
            action();
        }
    },

    /**
     * Performs navigation to specific page with optional delay
     * @param {number} page - Page number to navigate to
     * @param {boolean} useDelay - Whether to use delay for multi-column mode
     */
    performNavigationToPage(page, useDelay = false) {
        const action = () => {
            navigationUtils.scrollToPage(page);
            isPreviousPage = false;
            currentPage = getPageCount();
            this.updateCharacterPosition();
        };

        if (useDelay) {
            setTimeout(action, CONSTANTS.VIRTUAL_COLUMN_DELAY);
        } else {
            action();
        }
    }
};

// Expose utilities for legacy compatibility
const adjustVirtualColumns = appUtils.adjustVirtualColumns.bind(appUtils);
const updateCharacterPosition = appUtils.updateCharacterPosition.bind(appUtils);
const handleMessage = appUtils.handleMessage.bind(appUtils);
const handleNextCommand = () => commandHandlers.handleNext(domUtils.detectPlatform());
const handlePrevCommand = () => commandHandlers.handlePrev(domUtils.detectPlatform());
const getCharacterPositionFromScroll = characterUtils.getCharacterPositionFromScroll.bind(characterUtils);
const extractTextFromDocument = characterUtils.extractTextFromDocument.bind(characterUtils);

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

    // Add platform-specific classes
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
                setTimeout(adjustVirtualColumns, CONSTANTS.VIRTUAL_COLUMN_DELAY);
            }
           
            window.location.href = `${CONSTANTS.URLS.PAGE_LOAD}?true`;
        } catch (error) {
            console.error("Error during iframe onload:", error);
        }
    };

    // Set up event listeners
    window.addEventListener("message", event => handleMessage(event, platform));
    
    if (platform.isIOS) {
        window.addEventListener('touchstart', {});
    }
});

// Public API functions
function getWidth() {
    const contentWindow = domUtils.getContentWindow();
    if (!contentWindow) {
        console.warn("Iframe contentWindow not available in getWidth.");
        return 0;
    }
    return Math.floor(contentWindow.innerWidth);
}

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

function getCurrentPage() {
    return currentPage;
}

function gotoEnd() {
    if (isPreviousPage) {
        adjustVirtualColumns();
        const isSingleColumn = colCount === 1;
        
        if (isSingleColumn) {
            console.log("Single column mode detected, scrolling to end directly.");
            appUtils.performNavigationToEnd(false);
        } else {
            appUtils.performNavigationToEnd(true);
        }
    }
}

function setPreviousPage() {
    isPreviousPage = true;
    currentPage = 0;
}

function loadPage(page) {
    if (!frame) {
        console.error("Frame not found for loadPage.");
        return false;
    }
    
    console.log("Frame found. Loading page:", page);
    frame.setAttribute('src', page);
    currentPage = 0;
    return true;
}

function gotoPage(page) {
    console.log("Jumping to page:", page);
    
    if (page < 1) {
        console.warn("Page number must be 1 or greater. Current page:", page);
        return;
    }
    
    adjustVirtualColumns();
    const isSingleColumn = colCount === 1;
    
    if (isSingleColumn) {
        appUtils.performNavigationToPage(page, false);
    } else {
        appUtils.performNavigationToPage(page, true);
    }
}

function scrollToHorizontalEnd() {
    navigationUtils.scrollToHorizontalEnd();
}

function setReadiumProperty(property, value) {
    styleUtils.setReadiumProperty(property, value);
}

function unsetReadiumProperty(property) {
    styleUtils.unsetReadiumProperty(property);
}

function setBackgroundColor(color) {
    styleUtils.manageBackgroundColor(color);
}

function unsetBackgroundColor() {
    styleUtils.manageBackgroundColor(null);
}