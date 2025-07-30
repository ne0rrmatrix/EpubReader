/**
 * EPUB Reader JavaScript Interface
 * Handles rendering, navigation, and styling of EPUB content
 */

// Global state
let isPreviousPage = false;
let currentPage = 0;
let frame = null;
let colCount = 1;
let next = 'true';
let lastPageFlipTime = 0;
let minimumPageFlipDelay = 15000; // 15 seconds in milliseconds
let lastProcessedSpanId = null;
let autoPageFlipEnabled = true;
let pendingPageFlip = false;
let visibleSpanElements = [];
let oldHighlightSpanId = null;

document.addEventListener('selectstart', function (e) {
    e.preventDefault();
});

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
    },

    isCssVariableSet(element, variableName) {
        const computedStyle = getComputedStyle(element);
        const variableValue = computedStyle.getPropertyValue(variableName).trim();
        return variableValue !== '';
    },

    hasCssVariableChanged(element, variableName, oldValue) {
        const computedStyle = getComputedStyle(element);
        const variableValue = computedStyle.getPropertyValue(variableName).trim();
        if (variableValue === oldValue) {
            console.log(`CSS variable '${variableName}' has not changed: ${variableValue}`);
            return false;
        }
        else {
            console.log(`CSS variable '${variableName}' has changed from '${oldValue}' to '${variableValue}'`);
            return true;
        }
    },
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
        // Update visible spans after navigation
        setTimeout(updateVisibleSpanElements, 100);
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
        // Update visible spans after navigation
        setTimeout(updateVisibleSpanElements, 100);
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

        const hasCssVariableChanged = domUtils.hasCssVariableChanged(root, property, value);
        if (!hasCssVariableChanged) {
            console.log(`CSS variable '${property}' has not changed: ${value}`);
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
        const hasCssVariableChanged = domUtils.hasCssVariableChanged(document.documentElement, '--background-color', color);
        if (!hasCssVariableChanged) {
            console.log(`CSS variable '--background-color' has not changed: ${color}`);
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
    // Check if we should ignore this message due to a recent long press
    if (longPressUtils.shouldInterceptEvents()) {
        console.log("Ignoring message event due to recent long press");
        return;
    }
    
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
    // Make sure to update visible spans when navigating
    updateVisibleSpanElements();
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
    // Make sure to update visible spans when navigating
    updateVisibleSpanElements();
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
 * Long press detection utilities
 */
const longPressUtils = {
    /**
     * Long press configuration
     */
    config: {
        pressTimeout: 800, // Time in ms to consider a press as "long"
        moveTolerance: 10, // Maximum movement in pixels allowed during press
        preventEventsDuration: 1500 // How long to block events after long press (ms)
    },
    
    /**
     * Current state of touch/mouse tracking
     */
    state: {
        startTime: 0,
        startX: 0,
        startY: 0,
        target: null,
        timeoutId: null,
        active: false,
        longPressDetected: false, // Flag to track if a long press was detected
        preventClickUntil: 0,     // Timestamp until which to prevent click/touch events
        isAudioPlaying: false,    // Flag to track if audio is playing
        movedBeyondTolerance: false // Flag to track if movement exceeded tolerance
    },
    
    /**
     * Initializes long press detection on the given document
     * @param {Document} doc - Document to attach listeners to
     */
    initialize(doc) {
        if (!doc) {
            console.warn("Cannot initialize long press detection - document not available");
            return;
        }
        
        console.log("Initializing long press detection");
        
        // Touch events - use capture phase to ensure we get events first
        doc.addEventListener('touchstart', this.handleTouchStart.bind(this), { passive: false, capture: true });
        doc.addEventListener('touchmove', this.handleTouchMove.bind(this), { passive: false, capture: true });
        doc.addEventListener('touchend', this.handleTouchEnd.bind(this), { passive: false, capture: true });
        doc.addEventListener('touchcancel', this.handleTouchCancel.bind(this), { passive: false, capture: true });
        
        // Mouse events for desktop - use capture phase
        doc.addEventListener('mousedown', this.handleMouseDown.bind(this), { capture: true });
        doc.addEventListener('mousemove', this.handleMouseMove.bind(this), { capture: true });
        doc.addEventListener('mouseup', this.handleMouseUp.bind(this), { capture: true });
        
        // Add click interceptor with high priority (capture phase)
        doc.addEventListener('click', this.interceptClickEvent.bind(this), { capture: true });
        doc.addEventListener('touchend', this.interceptTouchEndEvent.bind(this), { capture: true });
        
        console.log("Long press detection initialized with click/touch interception");
    },
    
    /**
     * Sets audio playback state
     * @param {boolean} isPlaying - Whether audio is currently playing
     */
    setAudioPlaybackState(isPlaying) {
        this.state.isAudioPlaying = isPlaying;
        console.log(`Audio playback state set to: ${isPlaying}`);
    },
    
    /**
     * Intercepts click events and prevents them if a long press was recently detected
     * @param {MouseEvent} event - The click event
     */
    interceptClickEvent(event) {
        if (this.shouldInterceptEvents()) {
            console.log("Intercepted and prevented click event after long press");
            event.stopPropagation();
            event.preventDefault();
            return false;
        }
        return true;
    },
    
    /**
     * Intercepts touch end events and prevents them if a long press was recently detected
     * @param {TouchEvent} event - The touch end event
     */
    interceptTouchEndEvent(event) {
        if (this.shouldInterceptEvents()) {
            console.log("Intercepted and prevented touch end event after long press");
            event.stopPropagation();
            event.preventDefault();
            return false;
        }
        return true;
    },
    
    /**
     * Finds the closest span element with an ID to the click/touch position
     * @param {Element} element - Starting element to search from
     * @param {number} x - Click/touch X coordinate
     * @param {number} y - Click/touch Y coordinate
     * @returns {Element|null} The closest span with ID or null
     */
    findClosestSpanWithId(element, x, y) {
        if (!element) return null;
        
        // Get the document to search in
        const doc = element.ownerDocument || document;
        
        // First, check if the element itself is a span with ID
        if (element?.tagName === 'SPAN' && element.id) {
            return element;
        }
        
        // Find all spans with IDs in the document
        const allSpans = doc.querySelectorAll('span[id]');
        if (!allSpans.length) return null;
        
        // If we have only one span, return it
        if (allSpans.length === 1) return allSpans[0];
        
        // Calculate distances and find the closest span
        let closestSpan = null;
        let closestDistance = Number.MAX_VALUE;
        
        allSpans.forEach(span => {
            const rect = span.getBoundingClientRect();
            
            // Find center point of the span
            const centerX = rect.left + rect.width / 2;
            const centerY = rect.top + rect.height / 2;
            
            // Calculate Euclidean distance to click/touch position
            const distance = Math.sqrt(
                Math.pow(centerX - x, 2) + 
                Math.pow(centerY - y, 2)
            );
            
            // Update if this span is closer than the current closest
            if (distance < closestDistance) {
                closestDistance = distance;
                closestSpan = span;
            }
        });
        
        return closestSpan;
    },
    
    /**
     * Starts the long press timer
     * @param {Element} target - The target element
     * @param {number} x - Starting X coordinate
     * @param {number} y - Starting Y coordinate
     */
    startLongPressTimer(target, x, y) {
        this.clearLongPressTimer();
        
        this.state.startTime = Date.now();
        this.state.startX = x;
        this.state.startY = y;
        this.state.target = target;
        this.state.active = true;
        this.state.longPressDetected = false;
        this.state.movedBeyondTolerance = false;
        
        this.state.timeoutId = setTimeout(() => {
            this.handleLongPress();
        }, this.config.pressTimeout);
    },
    
    /**
     * Clears the long press timer
     */
    clearLongPressTimer() {
        if (this.state.timeoutId) {
            clearTimeout(this.state.timeoutId);
            this.state.timeoutId = null;
        }
        this.state.active = false;
    },
    
    /**
     * Handles long press detection completion
     */
    handleLongPress() {
        if (!this.state.active || this.state.movedBeyondTolerance) return;
        
        const spanElement = this.findClosestSpanWithId(
            this.state.target, 
            this.state.startX, 
            this.state.startY
        );
        
        if (spanElement?.id) {
            console.log(`Long press detected on span with ID: ${spanElement.id}`);
            
            // Set flags to prevent subsequent click/touch events
            this.state.longPressDetected = true;
            
            // Set longer prevention time during audio playback to ensure it works
            const preventDuration = this.state.isAudioPlaying ? 
                this.config.preventEventsDuration * 2 : // Double the duration for audio playback
                this.config.preventEventsDuration;
                
            this.state.preventClickUntil = Date.now() + preventDuration;
            
            // Notify C# code about the long press on a span
            window.location.href = `https://runcsharp.longpress?${spanElement.id}`;
        } else {
            console.log("Long press detected but no span with ID found");
            
            // Still prevent clicks even if no span was found
            this.state.longPressDetected = true;
            this.state.preventClickUntil = Date.now() + this.config.preventEventsDuration;
        }
        
        this.clearLongPressTimer();
    },
    
    /**
     * Handles touch start event
     * @param {TouchEvent} event - The touch start event
     */
    handleTouchStart(event) {
        if (event.touches.length !== 1) return; // Only track single touches
        
        const touch = event.touches[0];
        this.startLongPressTimer(event.target, touch.clientX, touch.clientY);
    },
    
    /**
     * Handles touch move event
     * @param {TouchEvent} event - The touch move event
     */
    handleTouchMove(event) {
        if (!this.state.active || event.touches.length !== 1) return;
        
        const touch = event.touches[0];
        
        // Check if touch has moved beyond tolerance
        const moveX = Math.abs(touch.clientX - this.state.startX);
        const moveY = Math.abs(touch.clientY - this.state.startY);
        
        if (moveX > this.config.moveTolerance || moveY > this.config.moveTolerance) {
            this.state.movedBeyondTolerance = true;
            
            // During audio playback, still block clicks even if moved
            if (this.state.isAudioPlaying) {
                this.state.longPressDetected = true;
                this.state.preventClickUntil = Date.now() + this.config.preventEventsDuration;
                console.log("Movement detected during audio playback - still blocking short touches");
            } else {
                this.clearLongPressTimer();
            }
        }
    },
    
    /**
     * Handles touch end event
     * @param {TouchEvent} event - The touch end event
     */
    handleTouchEnd(event) {
        // Check if we're in a long press state and should prevent default behavior
        if (this.shouldInterceptEvents()) {
            console.log("Preventing default touch end action due to recent long press");
            event.stopPropagation();
            event.preventDefault();
            return;
        }
        
        // Only clear timer if it wasn't a long press
        if (!this.state.longPressDetected) {
            this.clearLongPressTimer();
        }
        this.state.longPressDetected = false;
    },
    
    /**
     * Handles touch cancel event
     * @param {TouchEvent} event - The touch cancel event
     */
    handleTouchCancel(event) {
        this.clearLongPressTimer();
        this.state.longPressDetected = false;
    },
    
    /**
     * Handles mouse down event
     * @param {MouseEvent} event - The mouse down event
     */
    handleMouseDown(event) {
        if (event.button !== 0) return; // Only track left mouse button
        
        this.startLongPressTimer(event.target, event.clientX, event.clientY);
    },
    
    /**
     * Handles mouse move event
     * @param {MouseEvent} event - The mouse move event
     */
    handleMouseMove(event) {
        if (!this.state.active) return;
        
        // Check if mouse has moved beyond tolerance
        const moveX = Math.abs(event.clientX - this.state.startX);
        const moveY = Math.abs(event.clientY - this.state.startY);
        
        if (moveX > this.config.moveTolerance || moveY > this.config.moveTolerance) {
            this.state.movedBeyondTolerance = true;
            
            // For mouse movement, still block clicks even if moved beyond tolerance
            // This enables drag operations to still block click events
            this.state.longPressDetected = true;
            this.state.preventClickUntil = Date.now() + this.config.preventEventsDuration;
            console.log("Mouse moved beyond tolerance - blocking short clicks");
            
            // Only clear the timer (to prevent long press), but keep blocking clicks
            this.clearLongPressTimer();
        }
    },
    
    /**
     * Handles mouse up event
     * @param {MouseEvent} event - The mouse up event
     */
    handleMouseUp(event) {
        // Check if we're in a long press state and should prevent default behavior
        if (this.shouldInterceptEvents()) {
            console.log("Preventing default mouse up action due to recent long press");
            event.stopPropagation();
            event.preventDefault();
            return;
        }
        
        // Only clear timer if it wasn't a long press
        if (!this.state.longPressDetected) {
            this.clearLongPressTimer();
        }
        this.state.longPressDetected = false;
    },
    
    /**
     * Checks if interception is currently active
     * @returns {boolean} True if clicks/touches should be intercepted
     */
    shouldInterceptEvents() {
        const shouldIntercept = Date.now() < this.state.preventClickUntil;
        if (shouldIntercept) {
            console.log("Blocking events due to recent long press or movement");
        }
        return shouldIntercept;
    }
};

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
    if (!domUtils.isCssVariableSet(root, '--root-width') || !domUtils.isCssVariableSet(root, '--root-height')) {
        console.log("Root dimensions already set, skipping initial resize.");
        layoutUtils.setDimensions(body, root);
    }
    
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

            // Initialize long press detection on the iframe document
            longPressUtils.initialize(contentWindow.document);

            window.location.href = 'https://runcsharp.pageLoad?true';
        } catch (error) {
            console.error("Error during iframe onload:", error);
        }
        finally {
            // Inside frame.onload function, before the last line
            setTimeout(() => {
                updateVisibleSpanElements();
            }, 200);
        }
    };

    // Listen for messages from the parent window
    window.addEventListener("message", event => handleMessage(event, platform));
    if (platform.isIOS) {
        window.addEventListener('touchstart', {});
    }
    
    // Initialize long press detection on the main document as well
    longPressUtils.initialize(document);
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

function highlightSpan(spanId) {
    const doc = domUtils.getIframeDocument();
    const spanElement = doc.getElementById(spanId);
    removeHighlight(oldHighlightSpanId); // Remove previous highlight if any
    oldHighlightSpanId = spanId;
    console.log(`Highlighting span with ID: ${spanId}`);
    spanElement.style.backgroundColor = 'yellow';
    spanElement.style.fontWeight = 'bold';
}

function removeHighlight(spanId) {
    const doc = domUtils.getIframeDocument();
    if (!doc) {
       
        console.warn("Cannot access iframe document for removing highlight");
        return;
    }
    if (oldHighlightSpanId) {
        console.warn(`removing old highlight ID: ${oldHighlightSpanId}`);
        const oldSpanElement = doc.getElementById(oldHighlightSpanId);
        if (oldSpanElement) {
            oldSpanElement.style.backgroundColor = '';
            oldSpanElement.style.fontWeight = '';
        }
    }
    const spanElement = doc.getElementById(spanId);
    if (spanElement) {
        console.log(`Removing highlight from span with ID: ${spanId}`);
        spanElement.style.backgroundColor = '';
        spanElement.style.fontWeight = '';
    }
    else {
        console.warn(`Span with ID '${spanId}' not found for removing highlight in iframe.`);
    }
}
function handleNext() {
    console.log("Handling next command");
    navigationUtils.scrollRight(domUtils.detectPlatform());
}

/**
* Collects all span elements with IDs on all pages and organizes them by page number
* @returns {Object} Object with page numbers as keys and arrays of span elements as values
*/
function collectSpansByPage() {
    const contentWindow = domUtils.getContentWindow();
    const doc = domUtils.getIframeDocument();

    if (!contentWindow || !doc) {
        console.warn("Cannot access iframe document to collect spans by page");
        return {};
    }

    // Calculate page dimensions
    const pageWidth = contentWindow.innerWidth;
    const totalWidth = doc.documentElement.scrollWidth;
    const totalPages = Math.ceil(totalWidth / pageWidth);

    // Initialize result object with empty arrays for each page
    const spansByPage = {};
    for (let i = 0; i < totalPages; i++) {
        spansByPage[i] = [];
    }

    // Get all spans with IDs in the document
    const allSpans = doc.querySelectorAll('span[id]');

    // For each span, determine which page it belongs to
    allSpans.forEach(span => {
        const rect = span.getBoundingClientRect();

        // Calculate horizontal position relative to the document
        // Need to add scroll position to get absolute position
        const absoluteLeft = rect.left + contentWindow.scrollX;

        // Determine page number based on position
        const pageNumber = Math.floor(absoluteLeft / pageWidth);

        // Only include if page number is valid
        if (pageNumber >= 0 && pageNumber < totalPages) {
            spansByPage[pageNumber].push({
                id: span.id,
                text: span.textContent,
                rect: {
                    left: rect.left,
                    top: rect.top,
                    right: rect.right,
                    bottom: rect.bottom,
                    width: rect.width,
                    height: rect.height
                },
                absolutePosition: {
                    left: absoluteLeft,
                    top: rect.top + contentWindow.scrollY
                }
            });
        }
    });

    return spansByPage;
}

/**
 * Updates the global visibleSpanElements array with spans on the current page
 * @returns {Array} Array of span elements visible on current page
 */
function updateVisibleSpanElements() {
    const contentWindow = domUtils.getContentWindow();

    if (!contentWindow) {
        console.warn("Cannot access iframe content window to update visible spans");
        visibleSpanElements = [];
        return visibleSpanElements;
    }

    // Get all spans organized by page
    const spansByPage = collectSpansByPage();

    // Determine current page based on scroll position
    const pageWidth = contentWindow.innerWidth;
    const currentPageNumber = Math.floor(contentWindow.scrollX / pageWidth);

    // Get spans for current page
    visibleSpanElements = spansByPage[currentPageNumber] || [];

    return visibleSpanElements;
}

/**
 * Gets all spans organized by page
 * @returns {Object} Object with page numbers as keys and arrays of span elements as values
 */
function getAllSpansByPage() {
    return collectSpansByPage();
}

/**
 * Gets spans for a specific page number
 * @param {number} pageNumber - The page number to get spans for
 * @returns {Array} Array of span elements on the specified page
 */
function getSpansForPage(pageNumber) {
    const spansByPage = collectSpansByPage();
    return spansByPage[pageNumber] || [];
}

/**
* Checks if a span ID exists on the current page or the next page
* @param {string} spanId - The ID of the span to check
* @returns {boolean|null} - false if on current page, true if on next page, null if error or not found
*/
function isSpanOnNextPage(spanId) {
    try {
        // Make sure we have the most up-to-date span information
        const spansByPage = collectSpansByPage();
        const contentWindow = domUtils.getContentWindow();

        if (!contentWindow || !spansByPage) {
            console.warn("Cannot check span location - iframe content not accessible");
            return null;
        }
        console.log(`Checking span location for ID: ${spanId}`);
        // Determine current page based on scroll position
        const pageWidth = contentWindow.innerWidth;
        const currentPageNumber = Math.floor(contentWindow.scrollX / pageWidth);
        const nextPageNumber = currentPageNumber + 1;

        // Get spans for current and next pages
        const currentPageSpans = spansByPage[currentPageNumber] || [];
        const nextPageSpans = spansByPage[nextPageNumber] || [];

        // Check if spanId exists on current page
        const isOnCurrentPage = currentPageSpans.some(span => span.id === spanId);
        if (isOnCurrentPage) {
            return "false";
        }

        // Check if spanId exists on next page
        const isOnNextPage = nextPageSpans.some(span => span.id === spanId);
        if (isOnNextPage) {
            return "true";
        }

        // If not found on either page, check if it exists at all
        for (let pageNum in spansByPage) {
            if (spansByPage[pageNum].some(span => span.id === spanId)) {
                console.log(`Span ID '${spanId}' found on page ${pageNum} (not current or next)`);
                return "";
            }
        }

        // If we got here, the span doesn't exist
        console.warn(`Span ID '${spanId}' not found on any page`);
        return null;
    } catch (error) {
        console.error(`Error checking span location for ID '${spanId}':`, error);
        return null;
    }
}

function nextChapter() {
    console.log("Next chapter command received, navigating to next page.");
    window.location.href = 'https://runcsharp.next?true';
}

/**
 * Set whether automatic page flipping should be enabled
 * @param {string} enabled - Whether auto page flip is enabled ('true' or 'false')
 */
function setAutoPageFlip(enabled) {
    autoPageFlipEnabled = enabled === 'true';
    console.log(`Auto page flip ${autoPageFlipEnabled ? 'enabled' : 'disabled'}`);
    
    // Update long press utils with audio playback state
    longPressUtils.setAudioPlaybackState(autoPageFlipEnabled);
}