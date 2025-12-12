/**
 * EPUB Reader JavaScript Interface
 * Handles rendering, navigation, and styling of EPUB content
 */

// Global state
let isPreviousPage = false;
let currentPage = 0;
let frame = null;
let colCount = 1;

const mediaOverlayUi = {
    root: null,
    container: null,
    eyebrow: null,
    title: null,
    narrator: null,
    duration: null,
    status: null,
    toggleButton: null,
    minimizeButton: null,
    playButton: null,
    prevButton: null,
    nextButton: null,
    minimized: false,
    state: {
        supported: false,
        enabled: false,
        playing: false,
        narrator: '',
        durationSeconds: null,
        chapterTitle: '',
        segmentIndex: 0,
        segmentCount: 0,
        positionSeconds: null,
        seeking: false
    }
};

const mediaOverlayHighlight = {
    activeClass: null,
    playbackClass: null,
    elements: []
};

const mediaOverlayThemeDefaults = {
    highlightBackground: 'rgba(79, 224, 181, 0.28)',
    highlightText: '#041b15',
    highlightOutline: 'rgba(79, 224, 181, 0.65)'
};

const mediaOverlayThemeState = { ...mediaOverlayThemeDefaults };
let highlightThemeDirty = true;

function logMediaOverlay(eventName, details) {
    const entry = {
        event: eventName || 'unknown',
        details: details ?? null,
        timestamp: new Date().toISOString()
    };
    console.log('[MediaOverlay]', entry);
    try {
        sendToNativeMessage({ action: 'mediaoverlaylog', message: JSON.stringify(entry) });
    } catch (error) {
        console.warn('Failed to forward media overlay log to native layer', error);
    }
}

// New: navigation state to prevent interrupted smooth scrolling
const navigationState = {
    isAnimating: false,
    pending: null // 'next' | 'prev' | null
};

// Rate limit media-overlay navigation to avoid thrashing during smooth scrolls
const mediaOverlayNavigationDebounceMs = 600;
let mediaOverlayNavigationCooldownUntil = 0;
const mediaOverlayAutoAdvanceCooldownMs = 1500;
let mediaOverlayAutoAdvanceCooldownUntil = 0;

function isUserNavigationLocked() {
    return mediaOverlayUi.state.enabled && mediaOverlayUi.state.playing;
}

function shouldBlockMediaOverlayNavigation() {
    if (navigationState.isAnimating) {
        logMediaOverlay('Navigation ignored while animating');
        return true;
    }

    const now = Date.now();
    if (now < mediaOverlayNavigationCooldownUntil) {
        logMediaOverlay('Navigation ignored by cooldown', { remainingMs: mediaOverlayNavigationCooldownUntil - now });
        return true;
    }

    mediaOverlayNavigationCooldownUntil = now + mediaOverlayNavigationDebounceMs;
    return false;
}

function shouldThrottleMediaOverlayAutoAdvance() {
    if (!mediaOverlayUi.state.playing) {
        return false;
    }

    const now = Date.now();
    if (now < mediaOverlayAutoAdvanceCooldownUntil) {
        logMediaOverlay('Auto-advance ignored by cooldown', { remainingMs: mediaOverlayAutoAdvanceCooldownUntil - now });
        return true;
    }

    mediaOverlayAutoAdvanceCooldownUntil = now + mediaOverlayAutoAdvanceCooldownMs;
    return false;
}

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

function requestMediaOverlayHighlightThemeRefresh() {
    highlightThemeDirty = true;
    applyMediaOverlayHighlightTheme();
}

function applyMediaOverlayHighlightTheme() {
    if (!highlightThemeDirty) {
        return;
    }

    const doc = domUtils.getIframeDocument();
    if (!doc?.head) {
        return;
    }

    highlightThemeDirty = false;

    const styleId = 'mediaOverlayHighlightTheme';
    let styleNode = doc.getElementById(styleId);
    if (!styleNode) {
        styleNode = doc.createElement('style');
        styleNode.id = styleId;
        styleNode.dataset.generatedBy = 'media-overlay';
        doc.head.appendChild(styleNode);
    }

    const activeSelector = buildClassSelector(mediaOverlayHighlight.activeClass || '-epub-media-overlay-active');
    const playbackSelector = buildClassSelector(mediaOverlayHighlight.playbackClass || '-epub-media-overlay-playing');

    const highlightBackground = mediaOverlayThemeState.highlightBackground || mediaOverlayThemeDefaults.highlightBackground;
    const highlightText = mediaOverlayThemeState.highlightText || mediaOverlayThemeDefaults.highlightText;
    const highlightOutline = mediaOverlayThemeState.highlightOutline || mediaOverlayThemeDefaults.highlightOutline;

    const rules = [];
    if (activeSelector) {
        rules.push(`${activeSelector} {
    background-color: ${highlightBackground};
    color: ${highlightText};
    transition: background-color 0.18s ease, color 0.18s ease;
}`);
    }

    if (playbackSelector) {
        rules.push(`${playbackSelector} {
    box-shadow: 0 0 0 2px ${highlightOutline};
}`);
    }

    styleNode.textContent = rules.join('\n');
}

/**
 * Sets whether user interaction (scrolling, clicking) is enabled on the iframe
 * @param {any} enabled
 */
function setInteractionEnabled(enabled) {
    if (frame) {
        frame.style.pointerEvents = enabled ? 'auto' : 'none';
    }
}

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
     * Smoothly (or instantly) scroll to targetLeft; resolves when movement is done.
     * Uses 'scrollend' if available, falls back to proximity + timeout.
     * @param {Window} contentWindow
     * @param {number} targetLeft
     * @param {Object} platform - Platform flags
     * @returns {Promise<void>}
     */
    animateTo(contentWindow, targetLeft, platform) {
        const target = Math.round(targetLeft);

        return new Promise((resolve) => {
            let done = false;
            const epsilon = 2; // px tolerance
            const cleanup = () => {
                if (done) return;
                done = true;
                contentWindow.removeEventListener('scroll', onScroll, { passive: true });
                // scrollend may not exist everywhere; remove won't hurt if not added
                contentWindow.removeEventListener('scrollend', onScrollEnd);
                clearTimeout(timerId);
                resolve();
            };
            const onScroll = () => {
                if (Math.abs(contentWindow.scrollX - target) <= epsilon) {
                    cleanup();
                }
            };
            const onScrollEnd = () => cleanup();

            contentWindow.addEventListener('scroll', onScroll, { passive: true });
            // Optional; supported in modern engines. If not supported, listener will be inert.
            contentWindow.addEventListener('scrollend', onScrollEnd);

            // Safety timeout in case neither 'scrollend' nor proximity triggers (UA dependent)
            const timerId = setTimeout(() => {
                // Snap to target to guarantee we finish aligned to a page
                contentWindow.scrollTo(target, 0);
                cleanup();
            }, 800);
            if (platform.isWindows) {
                // Windows Edge has issues with smooth scrolling; use instant
                contentWindow.scrollTo(target, 0);
            } else if (mediaOverlayUi.state?.enabled) {
                // During media overlay playback, use instant scrolling to avoid disrupting narration timing
                contentWindow.scrollTo(target, 0);
            } else {
                contentWindow.scrollTo({
                    left: target,
                    top: 0,
                    behavior: 'smooth'
                });
            }
        });
    },

    /**
     * Scrolls the content left by one page
     * @param {Object} platform - Platform flags
     */
    async scrollLeft(platform) {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) return;

        // Coalesce interactions while animating
        if (navigationState.isAnimating) {
            navigationState.pending = 'prev';
            return;
        }

        const scrollAmount = this.calculateScrollAmount(contentWindow);
        const targetLeft = contentWindow.scrollX - scrollAmount;

        navigationState.isAnimating = true;
        setInteractionEnabled(false);
        try {
            await this.animateTo(contentWindow, targetLeft, platform);
            if (currentPage > 0) {
                currentPage--;
            } else {
                console.warn("Already at the first page, cannot scroll left.");
            }
            console.log("Scrolled left to page:", currentPage);
            updateCharacterPosition();
        } finally {
            navigationState.isAnimating = false;
            setInteractionEnabled(true);
            this._drainPending();
        }
    },

    /**
     * Scrolls the content right by one page
     * @param {Object} platform - Platform flags
     */
    async scrollRight(platform) {
        const contentWindow = domUtils.getContentWindow();
        if (!contentWindow) return;

        // Coalesce interactions while animating
        if (navigationState.isAnimating) {
            navigationState.pending = 'next';
            return;
        }

        const scrollAmount = this.calculateScrollAmount(contentWindow);
        const targetLeft = contentWindow.scrollX + scrollAmount;

        navigationState.isAnimating = true;
        setInteractionEnabled(false);
        try {
            await this.animateTo(contentWindow, targetLeft, platform);
            currentPage++;
            console.log("Scrolled right to page:", currentPage);
            updateCharacterPosition();
        } finally {
            navigationState.isAnimating = false;
            setInteractionEnabled(true);
            this._drainPending();
        }
    },

    /**
     * Drain one pending navigation request (if any) after animation completes.
     * Ensures we don't spam multiple additional navigations.
     * @private
     */
    _drainPending() {
        const pending = navigationState.pending;
        navigationState.pending = null;
        if (!pending) return;

        // Re-evaluate edges at the time we execute the pending nav
        if (pending === 'next') {
            handleNextCommand();
        } else if (pending === 'prev') {
            handlePrevCommand();
        }
    },

    /**
     * Scrolls to a specific page in the iframe content
     * @param {number} page - The page number to scroll to (0-based index)
     * @returns {void}
     */
    scrollToPage(page) {
        const contentWindow = domUtils.getContentWindow();
        const contentDoc = domUtils.getIframeDocument();
        if (!contentWindow || !contentDoc) {
            console.warn("Cannot scroll to page - iframe not ready.");
            return;
        }

        const scrollAmount = this.calculateScrollAmount(contentWindow);
        const maxScrollLeft = contentDoc.documentElement.scrollWidth - contentDoc.documentElement.clientWidth;
        const target = Math.min(maxScrollLeft, Math.max(0, Math.round(page * scrollAmount)));

        contentWindow.scrollTo(target, 0);
    },

    /**
     * Calculates the scroll amount for one page
     * @param {Window} contentWindow - The iframe content window
     * @returns {number} The scroll amount in pixels
     */
    calculateScrollAmount(contentWindow) {
        const gap = Number.parseInt(
            globalThis.getComputedStyle(contentWindow.document.documentElement)
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
        if (frame.contentWindow?.document.readyState === 'complete') {
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
            return Number.parseInt(columnCount, 10);
        }

        if (columnWidth && columnWidth !== 'auto') {
            const width = Number.parseFloat(columnWidth);
            const containerWidth = contentWindow.innerWidth;
            const gap = Number.parseFloat(computedStyle.getPropertyValue('column-gap')) || 0;
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
        const computedStyle = globalThis.getComputedStyle(documentElement);
        const height = Number.parseFloat(computedStyle.height);

        return Number.isNaN(height) ? documentElement.scrollHeight : height;
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
    // Origin validation for security
    if (!securityUtils.validateOrigin(event.origin, platform)) {
        return;
    }

    const { data } = event;

    if (data.startsWith("jump.")) {
        const href = data.substring(5);
        console.log("Jumping to:", href);
        sendToNativeMessage({ action: 'jump', href: href });
    } else if (data === "next") {
        if (mediaOverlayUi.state.seeking) {
            logMediaOverlay('Ignoring next command while user is seeking');
            return;
        }
        if (isUserNavigationLocked()) {
            logMediaOverlay('User navigation blocked during playback', { action: 'next' });
            return;
        }
        handleNextCommand();
    } else if (data === "prev") {
        if (mediaOverlayUi.state.seeking) {
            logMediaOverlay('Ignoring prev command while user is seeking');
            return;
        }
        if (isUserNavigationLocked()) {
            logMediaOverlay('User navigation blocked during playback', { action: 'prev' });
            return;
        }
        handlePrevCommand();
    } else if (data === "menu") {
        console.log("Received menu command.");
        sendToNativeMessage({ action: 'menu' });
    }
}

function encodeForCSharp(str) {
    // Convert to UTF-8 bytes, then to base64
    const utf8Bytes = new TextEncoder().encode(str);
    let binary = '';
    for (const byte of utf8Bytes) {
        binary += String.fromCodePoint(byte);
    }
    return btoa(binary);
}

// Helper: send to native bridge on Android using base64-encoded JSON, fallback to location.href scheme
function sendToNativeMessage(obj) {
    try {
        const payload = (typeof obj === 'string') ? { action: obj } : obj;
        const json = JSON.stringify(payload);
        const platform = domUtils.detectPlatform();
        const base64Json = encodeForCSharp(json);
        console.log('Prepared native message payload:', base64Json);
        
        if (globalThis.jsBridge && platform.isAndroid) {
            console.info('Sending message to native bridge via window.NativeBridge.InvokeAction:', json);
            globalThis.jsBridge.sendMessageToCSharp(base64Json);
            return;
        }
        if(platform.isWindows){
            console.info('Sending message to native bridge via chrome.webview.postMessage:', json);
            globalThis.chrome.webview.postMessage(base64Json);
            return;
        }
        if(platform.isIOS || platform.isMac){
            console.info('Sending message to native bridge via window.webkit.messageHandlers.webwindowinterop.postMessage:', json);
            globalThis.webkit.messageHandlers.webwindowinterop.postMessage("base64:" + base64Json);
            return;
        }
        // If we reach here, bridge not found or calls failed. Use fallback URL scheme for compatibility.
        console.info('Using fallback URL scheme for native message:', json);
    } catch (ex) {
        console.error('sendToNativeMessage failed', ex);
    }
}

function ensureMediaOverlayRoot() {
    if (!mediaOverlayUi.root) {
        mediaOverlayUi.root = document.getElementById('mediaOverlayPlayerRoot');
        logMediaOverlay('Cached root element', { found: Boolean(mediaOverlayUi.root) });
    }
    return mediaOverlayUi.root;
}

function resetMediaOverlayDom() {
    logMediaOverlay('Resetting UI element references');
    mediaOverlayUi.container = null;
    mediaOverlayUi.eyebrow = null;
    mediaOverlayUi.title = null;
    mediaOverlayUi.narrator = null;
    mediaOverlayUi.duration = null;
    mediaOverlayUi.status = null;
    mediaOverlayUi.toggleButton = null;
    mediaOverlayUi.minimizeButton = null;
    if (mediaOverlayUi.dockButton) {
        mediaOverlayUi.dockButton.remove();
    }
    mediaOverlayUi.dockButton = null;
    mediaOverlayUi.playButton = null;
    mediaOverlayUi.prevButton = null;
    mediaOverlayUi.nextButton = null;
    mediaOverlayUi.minimized = false;
    // Clear seek-related references and state to avoid stale references when UI is rebuilt
    mediaOverlayUi.seekInput = null;
    mediaOverlayUi.timeLabel = null;
    if (mediaOverlayUi.state) {
        mediaOverlayUi.state.seeking = false;
    }
}

function ensureMediaOverlayUi() {
    const root = ensureMediaOverlayRoot();
    if (!root || !mediaOverlayUi.state.supported) {
        logMediaOverlay('Skipping UI creation', { hasRoot: Boolean(root), supported: mediaOverlayUi.state.supported });
        return null;
    }

    if (!mediaOverlayUi.container) {
        logMediaOverlay('Building media overlay UI container');
        buildMediaOverlayUi(root);
    }

    return mediaOverlayUi.container;
}

function buildMediaOverlayUi(root) {
    logMediaOverlay('Constructing DOM nodes');
    root.innerHTML = '';
    const container = document.createElement('section');
    container.className = 'media-overlay-player media-overlay-player--disabled';

    const header = document.createElement('div');
    header.className = 'media-overlay-player__header';

    const titleWrap = document.createElement('div');

    const eyebrow = document.createElement('p');
    eyebrow.className = 'media-overlay-player__eyebrow';
    eyebrow.textContent = 'Media Overlay';

    const title = document.createElement('p');
    title.className = 'media-overlay-player__title';
    title.textContent = '';

    titleWrap.append(eyebrow, title);

    const toggleButton = document.createElement('button');
    toggleButton.type = 'button';
    toggleButton.className = 'media-overlay-player__toggle';
    toggleButton.setAttribute('aria-pressed', 'false');
    toggleButton.textContent = 'Playback Disabled';
    toggleButton.addEventListener('click', handleMediaOverlayToggleClick);

    // Minimize/restore button
    const minimizeButton = document.createElement('button');
    minimizeButton.type = 'button';
    minimizeButton.className = 'media-overlay-player__minimize';
    minimizeButton.setAttribute('aria-label', 'Minimize media overlay');
    minimizeButton.textContent = '\u2013'; // en dash as minimize glyph
    minimizeButton.addEventListener('click', function (ev) {
        ev.stopPropagation();
        handleMediaOverlayMinimizeClick();
    });

    // Append minimize button near toggle for easy access
    const headerRight = document.createElement('div');
    headerRight.style.display = 'flex';
    headerRight.style.gap = '0.5rem';
    headerRight.append(minimizeButton, toggleButton);

    header.append(titleWrap, headerRight);

    const meta = document.createElement('div');
    meta.className = 'media-overlay-player__meta';
    // Use flex layout so the progress bar can expand and replace the
    // previously duplicated duration element that appeared near the center.
    meta.style.display = 'flex';
    meta.style.alignItems = 'center';
    meta.style.gap = '0.75rem';
    meta.style.width = '100%';

    const narrator = document.createElement('span');
    narrator.className = 'media-overlay-player__narrator';
    narrator.textContent = 'Narrator unavailable';
    narrator.style.flex = '0 0 auto';

    // Progress / seek bar
    const progressWrap = document.createElement('div');
    progressWrap.className = 'media-overlay-player__progress';

    const seekInput = document.createElement('input');
    seekInput.type = 'range';
    seekInput.className = 'media-overlay-player__seek';
    seekInput.min = '0';
    seekInput.max = '1';
    seekInput.step = '0.1';
    seekInput.value = '0';
    seekInput.disabled = true;

    const timeLabel = document.createElement('span');
    timeLabel.className = 'media-overlay-player__time';
    timeLabel.textContent = '';

    // Seek interactions: set seeking flag while user drags; only send on change (release)
    // Helper: update the time label and in-memory position
    function _updateSeekPreviewTime(ratio) {
        const secs = mediaOverlayUi.state.durationSeconds ? (ratio * mediaOverlayUi.state.durationSeconds) : null;
        timeLabel.textContent = (secs == null ? '0:00' : formatDuration(secs)) + (mediaOverlayUi.state.durationSeconds ? ' / ' + formatDuration(mediaOverlayUi.state.durationSeconds) : '');
        if (secs != null) {
            mediaOverlayUi.state.positionSeconds = secs;
        }
    }

    // Helper: update highlight near viewport center
    function _applyPreviewHighlight(doc, contentWindow) {
        try {
            const cx = Math.floor(contentWindow.innerWidth / 2);
            const el = doc.elementFromPoint(cx, Math.floor(contentWindow.innerHeight / 2));
            if (!el || el.nodeType !== Node.ELEMENT_NODE) return;

            clearMediaOverlayHighlight();
            if (mediaOverlayHighlight.activeClass) el.classList.add(mediaOverlayHighlight.activeClass);
            if (mediaOverlayHighlight.playbackClass && mediaOverlayUi.state.playing) el.classList.add(mediaOverlayHighlight.playbackClass);
            mediaOverlayHighlight.elements = [el];
        } catch (e) {
            console.warn('Preview highlight failed', e);
        }
    }

    // Helper: perform live preview scroll and page snapping
    function _performLivePreviewScroll(ratio) {
        try {
            const contentWindow = domUtils.getContentWindow();
            const doc = domUtils.getIframeDocument();
            if (!contentWindow || !doc) return;

            const scrollElem = doc.scrollingElement || doc.documentElement;
            const maxScrollLeft = Math.max(0, scrollElem.scrollWidth - contentWindow.innerWidth);
            const scrollAmount = navigationUtils.calculateScrollAmount(contentWindow) || contentWindow.innerWidth;

            let desiredLeft = Math.round(ratio * maxScrollLeft);
            if (scrollAmount > 0) {
                const pageIndex = Math.round(desiredLeft / scrollAmount);
                const maxPageIndex = Math.max(0, Math.floor(maxScrollLeft / scrollAmount));
                const clampedIndex = Math.min(maxPageIndex, Math.max(0, pageIndex));
                desiredLeft = Math.round(clampedIndex * scrollAmount);
                currentPage = clampedIndex;
            }

            const platform = domUtils.detectPlatform();
            if (!navigationState.isAnimating) {
                navigationUtils.animateTo(contentWindow, desiredLeft, platform).catch(() => { });
            }

            _applyPreviewHighlight(doc, contentWindow);
        } catch (e) {
            console.warn('Live preview scroll failed', e);
        }
    }

    seekInput.addEventListener('input', function () {
        const ratio = Number(seekInput.value) || 0;
        _updateSeekPreviewTime(ratio);
        _performLivePreviewScroll(ratio);
    });
    seekInput.addEventListener('change', function () {
        if (!mediaOverlayUi.state.durationSeconds) return;
        const secs = Number(seekInput.value) * mediaOverlayUi.state.durationSeconds;
        // send seek command only when the user releases the thumb
        sendMediaOverlayCommand('mediaoverlayseek', { seconds: secs });
    });

    // Pointer/touch handlers to mark seeking state so native UI updates are deferred
    seekInput.addEventListener('pointerdown', function () {
        mediaOverlayUi.state.seeking = true;
    });
    seekInput.addEventListener('pointerup', function () {
        mediaOverlayUi.state.seeking = false;
    });
    seekInput.addEventListener('touchstart', function () {
        mediaOverlayUi.state.seeking = true;
    }, { passive: true });
    seekInput.addEventListener('touchend', function () {
        mediaOverlayUi.state.seeking = false;
    });
    // Also handle pointercancel/leave as end-of-drag to keep state consistent
    seekInput.addEventListener('pointercancel', function () {
        mediaOverlayUi.state.seeking = false;
    });
    seekInput.addEventListener('pointerleave', function () {
        // Only clear seeking on pointerleave if buttons are not pressed (best-effort)
        try {
            // Clear seeking state on pointer leave. Use a simple, deterministic
            // assignment instead of probing Event.prototype for broader
            // compatibility and to avoid redundant branches.
            mediaOverlayUi.state.seeking = false;
        } catch (e) {
            console.warn('Failed to clear seeking state on pointerleave', e);
            mediaOverlayUi.state.seeking = false;
        }
    });

    // Make the progress wrapper expand to use available horizontal space.
    progressWrap.style.display = 'flex';
    progressWrap.style.alignItems = 'center';
    progressWrap.style.gap = '0.5rem';
    progressWrap.style.flex = '1';

    // Let the range input grow to fill the wrapper and keep the time label compact.
    seekInput.style.flex = '1';
    seekInput.style.minWidth = '0';
    seekInput.style.width = '100%';
    timeLabel.style.whiteSpace = 'nowrap';
    timeLabel.style.flex = '0 0 auto';

    progressWrap.append(seekInput, timeLabel);
    // Append narrator and the expanded progress area to the meta row.
    meta.append(narrator, progressWrap);

    const controls = document.createElement('div');
    controls.className = 'media-overlay-player__controls';

    const status = document.createElement('span');
    status.className = 'media-overlay-player__status';
    status.textContent = 'Media overlay disabled';

    const buttonsWrapper = document.createElement('div');
    buttonsWrapper.className = 'media-overlay-player__buttons';

    const prevButton = document.createElement('button');
    prevButton.type = 'button';
    prevButton.textContent = '<<';
    prevButton.setAttribute('aria-label', 'Previous passage');
    prevButton.addEventListener('click', handleMediaOverlayPrevClick);

    const playButton = document.createElement('button');
    playButton.type = 'button';
    playButton.textContent = '>';
    playButton.setAttribute('aria-label', 'Play narration');
    playButton.addEventListener('click', handleMediaOverlayPlayClick);

    const nextButton = document.createElement('button');
    nextButton.type = 'button';
    nextButton.textContent = '>>';
    nextButton.setAttribute('aria-label', 'Next passage');
    nextButton.addEventListener('click', handleMediaOverlayNextClick);

    buttonsWrapper.append(prevButton, playButton, nextButton);
    controls.append(status, buttonsWrapper);

    container.append(header, meta, controls);
    root.appendChild(container);

    // Create a small docked restore button that is shown when minimized
    const dockButton = document.createElement('button');
    dockButton.type = 'button';
    dockButton.className = 'media-overlay-player__dock';
    dockButton.setAttribute('aria-label', 'Restore media overlay');
    dockButton.textContent = '\u25A3'; // square glyph as restore icon
    dockButton.style.display = 'none';
    dockButton.addEventListener('click', function (ev) {
        ev.stopPropagation();
        setMediaOverlayMinimized(false);
    });
    root.appendChild(dockButton);

    mediaOverlayUi.container = container;
    mediaOverlayUi.eyebrow = eyebrow;
    mediaOverlayUi.title = title;
    mediaOverlayUi.narrator = narrator;
    mediaOverlayUi.status = status;
    mediaOverlayUi.toggleButton = toggleButton;
    mediaOverlayUi.minimizeButton = minimizeButton;
    mediaOverlayUi.dockButton = dockButton;
    mediaOverlayUi.prevButton = prevButton;
    mediaOverlayUi.playButton = playButton;
    mediaOverlayUi.nextButton = nextButton;
    mediaOverlayUi.seekInput = seekInput;
    mediaOverlayUi.timeLabel = timeLabel;

    // container click restores when minimized (tap-to-restore)
    container.addEventListener('click', function (ev) {
        if (mediaOverlayUi.minimized) {
            ev.stopPropagation();
            setMediaOverlayMinimized(false);
        }
    });

    // Prevent underlying content from receiving taps when overlay is present
    root.style.pointerEvents = 'auto';
}

function setMediaOverlayMinimized(minimized) {
    const root = ensureMediaOverlayRoot();
    mediaOverlayUi.minimized = Boolean(minimized);
    if (root) {
        root.dataset.minimized = mediaOverlayUi.minimized ? 'true' : 'false';
    }
    // When minimized, visually hide the overlay container but keep it in
    // memory so state is preserved. The CSS handles the visual hiding
    // using the root[data-minimized] selector; we just make the dock
    // (restore) button visible so the user can restore the overlay.
    if (mediaOverlayUi.minimized) {
        if (mediaOverlayUi.dockButton) {
            mediaOverlayUi.dockButton.style.display = 'flex';
        }

        // Ensure root is visible so the dock button can be interacted with
        if (root) {
            root.dataset.visible = 'true';
            root.style.pointerEvents = 'auto';
        }
    } else {
        // Restoring: hide dock and recreate the full overlay UI
        if (mediaOverlayUi.dockButton) {
            mediaOverlayUi.dockButton.style.display = 'none';
        }
        // Rebuild container if needed
        ensureMediaOverlayUi();
        // Refresh the rebuilt UI from the in-memory state so metadata is shown
        try {
            updateMediaOverlayPlaybackState(mediaOverlayUi.state);
        } catch (e) {
            console.warn('Failed to refresh media overlay UI on restore', e);
        }
        if (root) {
            root.dataset.visible = 'true';
            root.style.pointerEvents = 'auto';
        }
    }

    logMediaOverlay('Minimized state changed', { minimized: mediaOverlayUi.minimized });
}

function handleMediaOverlayMinimizeClick() {
    setMediaOverlayMinimized(!mediaOverlayUi.minimized);
}

function sendMediaOverlayCommand(action, payload) {
    if (!action) {
        logMediaOverlay('Ignoring empty command request');
        return;
    }
    const message = { action: action };
    if (payload && typeof payload === 'object') {
        Object.assign(message, payload);
    }
    logMediaOverlay('Dispatching command', message);
    sendToNativeMessage(message);
}

function handleMediaOverlayToggleClick() {
    if (!mediaOverlayUi.state.supported) {
        logMediaOverlay('Toggle ignored, not supported');
        return;
    }
    const nextValue = !mediaOverlayUi.state.enabled;
    logMediaOverlay('Toggle pressed', { nextEnabled: nextValue });
    sendMediaOverlayCommand('mediaoverlaytoggle', { enabled: nextValue });
}

function handleMediaOverlayPlayClick() {
    if (!mediaOverlayUi.state.supported || !mediaOverlayUi.state.enabled || mediaOverlayUi.state.segmentCount === 0) {
        logMediaOverlay('Play/pause ignored', {
            supported: mediaOverlayUi.state.supported,
            enabled: mediaOverlayUi.state.enabled,
            segments: mediaOverlayUi.state.segmentCount
        });
        return;
    }
    const action = mediaOverlayUi.state.playing ? 'mediaoverlaypause' : 'mediaoverlayplay';
    logMediaOverlay('Play button pressed', { action: action });
    sendMediaOverlayCommand(action);
}

function getActiveMediaOverlayHighlightElement(doc) {
    const connectedHighlight = mediaOverlayHighlight.elements.find(element => element?.isConnected);
    if (connectedHighlight) {
        return connectedHighlight;
    }

    const selector = buildClassSelector(mediaOverlayHighlight.activeClass);
    if (selector && doc) {
        return doc.querySelector(selector);
    }

    return null;
}

function maybeNavigateToPreviousPageFromHighlightTop() {
    const doc = domUtils.getIframeDocument();
    if (!doc) {
        return;
    }

    const highlightElement = getActiveMediaOverlayHighlightElement(doc);
    if (!highlightElement) {
        return;
    }

    const rect = highlightElement.getBoundingClientRect();
    const topThresholdPx = 6;

    if (rect.top <= topThresholdPx) {
        logMediaOverlay('Highlight at top of page, paging previous', { top: rect.top });
        handlePrevCommand();
    }
}

function handleMediaOverlayPrevClick() {
    if (!mediaOverlayUi.state.supported || !mediaOverlayUi.state.enabled || mediaOverlayUi.state.segmentCount === 0) {
        logMediaOverlay('Prev ignored', {
            supported: mediaOverlayUi.state.supported,
            enabled: mediaOverlayUi.state.enabled,
            segments: mediaOverlayUi.state.segmentCount
        });
        return;
    }
    if (shouldBlockMediaOverlayNavigation()) {
        return;
    }

    logMediaOverlay('Prev requested');
    sendMediaOverlayCommand('mediaoverlayprev');
}

function handleMediaOverlayNextClick() {
    if (!mediaOverlayUi.state.supported || !mediaOverlayUi.state.enabled || mediaOverlayUi.state.segmentCount === 0) {
        logMediaOverlay('Next ignored', {
            supported: mediaOverlayUi.state.supported,
            enabled: mediaOverlayUi.state.enabled,
            segments: mediaOverlayUi.state.segmentCount
        });
        return;
    }
    if (shouldBlockMediaOverlayNavigation()) {
        return;
    }
    logMediaOverlay('Next requested');
    sendMediaOverlayCommand('mediaoverlaynext');
}

function setMediaOverlayVisibility(isSupported) {
    const root = ensureMediaOverlayRoot();
    if (!root) {
        return;
    }

    const visible = Boolean(isSupported);
    logMediaOverlay('Visibility updated', { supported: visible });
    mediaOverlayUi.state.supported = visible;
    root.dataset.visible = visible ? 'true' : 'false';

    if (!visible) {
        mediaOverlayUi.state.enabled = false;
        mediaOverlayUi.state.playing = false;
        mediaOverlayUi.state.segmentCount = 0;
        mediaOverlayUi.state.segmentIndex = 0;
        clearMediaOverlayHighlight();
        // clear minimized state when hiding
        root.dataset.minimized = 'false';
        mediaOverlayUi.minimized = false;
        root.innerHTML = '';
        resetMediaOverlayDom();
        return;
    }

    ensureMediaOverlayUi();
}

function setMediaOverlayReaderModeHidden(isHidden) {
    const root = ensureMediaOverlayRoot();
    if (!root) {
        return;
    }

    const hidden = Boolean(isHidden);
    root.dataset.readerModeHidden = hidden ? 'true' : 'false';
    logMediaOverlay('Reader mode chrome visibility updated', { hidden });

    if (!hidden && mediaOverlayUi.state.supported) {
        ensureMediaOverlayUi();
    }
}

function initializeMediaOverlayUi(config) {
    const normalized = config || {};
    logMediaOverlay('Initializing UI state', normalized);
    const root = ensureMediaOverlayRoot();
    if (!root) {
        return;
    }

    if (typeof normalized.segmentCount === 'number') {
        mediaOverlayUi.state.segmentCount = Math.max(0, Math.floor(normalized.segmentCount));
    }
    if (typeof normalized.enabled === 'boolean') {
        mediaOverlayUi.state.enabled = normalized.enabled;
    }
    mediaOverlayUi.state.narrator = normalized.narrator || '';
    mediaOverlayUi.state.durationSeconds = typeof normalized.durationSeconds === 'number' ? normalized.durationSeconds : null;
    mediaOverlayUi.state.chapterTitle = normalized.chapterTitle || mediaOverlayUi.state.chapterTitle;

    const container = ensureMediaOverlayUi();
    if (!container) {
        return;
    }

    if (mediaOverlayUi.title) {
        mediaOverlayUi.title.textContent = mediaOverlayUi.state.chapterTitle || 'Narrated Section';
    }

    if (mediaOverlayUi.narrator) {
        mediaOverlayUi.narrator.textContent = mediaOverlayUi.state.narrator
            ? 'Narrated by ' + mediaOverlayUi.state.narrator
            : 'Narrator unavailable';
    }

    if (mediaOverlayUi.duration) {
        const formatted = formatDuration(mediaOverlayUi.state.durationSeconds);
        mediaOverlayUi.duration.textContent = formatted ? 'Duration ' + formatted : '';
    }

    if (mediaOverlayUi.state.segmentCount === 0) {
        mediaOverlayUi.state.segmentIndex = 0;
    } else if (mediaOverlayUi.state.segmentIndex === 0) {
        mediaOverlayUi.state.segmentIndex = 1;
    }

    updateMediaOverlayPlaybackState({
        enabled: mediaOverlayUi.state.enabled,
        playing: false,
        segmentIndex: mediaOverlayUi.state.segmentIndex,
        segmentCount: mediaOverlayUi.state.segmentCount,
        chapterTitle: mediaOverlayUi.state.chapterTitle
    });
}

function updateMediaOverlayPlaybackState(state) {

    if (state) {
        updateUiStateFromPayload(state);
    }

    const container = ensureMediaOverlayUi();
    if (!container) return;

    updateUiTitle();
    updateUiToggleButton();
    updateUiNarrator();
    updateUiDuration();
    updateUiProgress();
    const disableControls = !mediaOverlayUi.state.enabled || mediaOverlayUi.state.segmentCount === 0;
    updateUiControls(disableControls);
    updateUiPlayButton();
    updateUiStatus();
    updateUiContainerClass(disableControls);
    applyPlaybackClassToHighlight();
}

function updateUiStateFromPayload(state) {
    if (!state) return;

    const s = mediaOverlayUi.state;

    // Helper: safely set a boolean property from payload
    const setBool = (prop) => {
        if (typeof state[prop] === 'boolean') s[prop] = state[prop];
    };

    // Helper: safely set an integer >= 0
    const setNonNegInt = (prop, target) => {
        if (typeof state[prop] === 'number') {
            s[target || prop] = Math.max(0, Math.floor(state[prop]));
        }
    };

    // Helper: set numeric-or-null fields when explicitly present in payload
    const setMaybeNumber = (prop, target) => {
        if (Object.hasOwn(state, prop)) {
            const v = state[prop];
            s[target || prop] = typeof v === 'number' ? v : null;
        }
    };

    setBool('enabled');
    setBool('playing');

    // Segment index: avoid overwriting while user is seeking
    if (typeof state.segmentIndex === 'number') {
        if (s.seeking) {
            logMediaOverlay('Skipping segmentIndex update while seeking', { incoming: state.segmentIndex });
        } else {
            s.segmentIndex = Math.max(0, Math.floor(state.segmentIndex));
        }
    }

    setNonNegInt('segmentCount');

    if (typeof state.chapterTitle === 'string' && state.chapterTitle.length > 0) {
        s.chapterTitle = state.chapterTitle;
    }

    setMaybeNumber('durationSeconds');

    if (Object.hasOwn(state, 'positionSeconds')) {
        if (s.seeking) {
            logMediaOverlay('Skipping positionSeconds update while seeking', { incoming: state.positionSeconds });
        } else {
            const v = state.positionSeconds;
            s.positionSeconds = typeof v === 'number' ? v : null;
        }
    }

    if (!s.playing) {
        mediaOverlayNavigationCooldownUntil = 0;
        mediaOverlayAutoAdvanceCooldownUntil = 0;
    }
}

function updateUiTitle() {
    if (mediaOverlayUi.title && mediaOverlayUi.state.chapterTitle) {
        mediaOverlayUi.title.textContent = mediaOverlayUi.state.chapterTitle;
    }
}

function updateUiToggleButton() {
    if (mediaOverlayUi.toggleButton) {
        mediaOverlayUi.toggleButton.setAttribute('aria-pressed', mediaOverlayUi.state.enabled ? 'true' : 'false');
        mediaOverlayUi.toggleButton.textContent = mediaOverlayUi.state.enabled ? 'Playable' : 'Playback Disabled';
    }
}

function updateUiNarrator() {
    if (mediaOverlayUi.narrator) {
        mediaOverlayUi.narrator.textContent = mediaOverlayUi.state.narrator
            ? 'Narrated by ' + mediaOverlayUi.state.narrator
            : 'Narrator unavailable';
    }
}

function updateUiDuration() {
    if (mediaOverlayUi.duration) {
        const formattedDuration = formatDuration(mediaOverlayUi.state.durationSeconds);
        mediaOverlayUi.duration.textContent = formattedDuration ? 'Duration ' + formattedDuration : '';
    }
}

function updateUiProgress() {
    // Simplified control flow: early returns and small helpers keep complexity low
    if (!mediaOverlayUi.seekInput) return;
    const input = mediaOverlayUi.seekInput;
    const label = mediaOverlayUi.timeLabel;
    const dur = mediaOverlayUi.state.durationSeconds;
    const pos = mediaOverlayUi.state.positionSeconds;
    const seeking = Boolean(mediaOverlayUi.state.seeking);

    const hasDuration = typeof dur === 'number' && !Number.isNaN(dur) && dur > 0;

    if (!hasDuration) {
        if (!seeking) {
            input.disabled = true;
            input.value = '0';
        }
        if (label) {
            label.textContent = dur ? formatDuration(dur) : '';
        }
        return;
    }

    input.disabled = false;
    if (!seeking) {
        input.value = String(computeProgressRatio(pos, dur));
    }
    if (label) {
        label.textContent = computeProgressLabel(pos, dur);
    }
}

/**
 * Helper: compute ratio (0..1) for given position/duration
 */
function computeProgressRatio(pos, dur) {
    if (typeof pos === 'number' && !Number.isNaN(pos) && typeof dur === 'number' && dur > 0) {
        return Math.max(0, Math.min(1, pos / dur));
    }
    return 0;
}

/**
 * Helper: build progress label text like "M:SS / M:SS"
 */
function computeProgressLabel(pos, dur) {
    const posText = (typeof pos === 'number' && !Number.isNaN(pos)) ? formatDuration(pos) : '0:00';
    return posText + ' / ' + formatDuration(dur);
}

function updateUiControls(disableControls) {
    [mediaOverlayUi.prevButton, mediaOverlayUi.playButton, mediaOverlayUi.nextButton].forEach(button => {
        if (button) {
            button.disabled = disableControls;
        }
    });
}

function updateUiPlayButton() {
    if (mediaOverlayUi.playButton) {
        mediaOverlayUi.playButton.textContent = mediaOverlayUi.state.playing ? '||' : '>';
        mediaOverlayUi.playButton.setAttribute('aria-label', mediaOverlayUi.state.playing ? 'Pause narration' : 'Play narration');
    }
}

function updateUiStatus() {
    if (mediaOverlayUi.status) {
        if (mediaOverlayUi.state.segmentCount === 0) {
            mediaOverlayUi.status.textContent = mediaOverlayUi.state.enabled
                ? 'Audio not available for this section'
                : 'Media overlay disabled';
        } else {
            const index = mediaOverlayUi.state.segmentIndex > 0 ? mediaOverlayUi.state.segmentIndex : 1;
            mediaOverlayUi.status.textContent = 'Segment ' + index + ' of ' + mediaOverlayUi.state.segmentCount;
        }
    }
}

function updateUiContainerClass(disableControls) {
    if (mediaOverlayUi.container) {
        mediaOverlayUi.container.classList.toggle('media-overlay-player--disabled', disableControls);
    }
}

function highlightMediaOverlayFragment(fragmentId, activeClass, playbackClass) {
    let selectorsChanged = false;
    if (activeClass && mediaOverlayHighlight.activeClass !== activeClass) {
        mediaOverlayHighlight.activeClass = activeClass;
        selectorsChanged = true;
    }
    if (playbackClass && mediaOverlayHighlight.playbackClass !== playbackClass) {
        mediaOverlayHighlight.playbackClass = playbackClass;
        selectorsChanged = true;
    }
    if (selectorsChanged) {
        requestMediaOverlayHighlightThemeRefresh();
    }

    const doc = domUtils.getIframeDocument();
    if (!doc || !fragmentId) {
        return;
    }

    clearMediaOverlayHighlight(activeClass, playbackClass);

    const target = doc.getElementById(fragmentId);
    if (!target) {
        console.warn('Media overlay fragment not found:', fragmentId);
        return;
    }

    if (mediaOverlayHighlight.activeClass) {
        target.classList.add(mediaOverlayHighlight.activeClass);
    }
    if (mediaOverlayHighlight.playbackClass && mediaOverlayUi.state.playing) {
        target.classList.add(mediaOverlayHighlight.playbackClass);
    }

    mediaOverlayHighlight.elements = [target];

    // Page advancement is handled by C# MediaOverlayPlaybackManager
    // to prevent race conditions and duplicate page navigation.
    // The C# code calls handleNextCommand() when needed before highlighting.
}

function clearMediaOverlayHighlight(activeClass, playbackClass) {

    let selectorsChanged = false;
    if (activeClass && mediaOverlayHighlight.activeClass !== activeClass) {
        mediaOverlayHighlight.activeClass = activeClass;
        selectorsChanged = true;
    }
    if (playbackClass && mediaOverlayHighlight.playbackClass !== playbackClass) {
        mediaOverlayHighlight.playbackClass = playbackClass;
        selectorsChanged = true;
    }
    if (selectorsChanged) {
        requestMediaOverlayHighlightThemeRefresh();
    }

    const doc = domUtils.getIframeDocument();
    const active = mediaOverlayHighlight.activeClass;
    const playback = mediaOverlayHighlight.playbackClass;

    mediaOverlayHighlight.elements.forEach(element => {
        if (!element) {
            return;
        }
        if (active) {
            element.classList.remove(active);
        }
        if (playback) {
            element.classList.remove(playback);
        }
    });

    mediaOverlayHighlight.elements = [];

    if (!doc) {
        return;
    }

    [active, playback]
        .filter(Boolean)
        .map(buildClassSelector)
        .filter(Boolean)
        .forEach(selector => {
            doc.querySelectorAll(selector).forEach(node => {
                if (active) {
                    node.classList.remove(active);
                }
                if (playback) {
                    node.classList.remove(playback);
                }
            });
        });
}

function applyPlaybackClassToHighlight() {
    if (!mediaOverlayHighlight.playbackClass || mediaOverlayHighlight.elements.length === 0) {
        logMediaOverlay('No elements to update for playback class');
        return;
    }

    mediaOverlayHighlight.elements = mediaOverlayHighlight.elements.filter(element => element?.isConnected);
    mediaOverlayHighlight.elements.forEach(element => {
        if (!element) {
            return;
        }
        if (mediaOverlayUi.state.playing) {
            element.classList.add(mediaOverlayHighlight.playbackClass);
        } else {
            element.classList.remove(mediaOverlayHighlight.playbackClass);
        }
    });
}

function buildClassSelector(className) {
    if (!className) {
        return null;
    }

    if (globalThis.CSS && typeof globalThis.CSS.escape === 'function') {
        return '.' + globalThis.CSS.escape(className);
    }

    // Use String.raw to avoid escaping backslashes
    return '.' + className.replaceAll(/([^a-zA-Z0-9_-])/g, String.raw`\$1`);
}

function formatDuration(seconds) {
    if (seconds == null) {
        return '';
    }

    const totalSeconds = Math.max(0, Math.floor(Number(seconds)));
    if (Number.isNaN(totalSeconds)) {
        return '';
    }

    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const remainingSeconds = totalSeconds % 60;

    const twoDigit = value => (value < 10 ? '0' + value : String(value));
    if (hours > 0) {
        return hours + ':' + twoDigit(minutes) + ':' + twoDigit(remainingSeconds);
    }
    return minutes + ':' + twoDigit(remainingSeconds);
}

/**
 * Handles the "next" command
 */
function handleNextCommand() {
    if (mediaOverlayUi.state.seeking) {
        logMediaOverlay('handleNextCommand ignored while seeking');
        return;
    }
    const platform = domUtils.detectPlatform();

    if (mediaOverlayUi.state.playing && shouldThrottleMediaOverlayAutoAdvance()) {
        return;
    }

    // If animation in progress, coalesce to a single pending "next"
    if (navigationState.isAnimating) {
        navigationState.pending = 'next';
        return;
    }

    if (navigationUtils.isHorizontallyScrolledToEnd()) {
        console.log("Reached end of current content, requesting next page.");
        sendToNativeMessage({ action: 'next' });
    } else {
        navigationUtils.scrollRight(platform);
    }
}

/**
 * Handles the "prev" command
 */
function handlePrevCommand() {
    if (mediaOverlayUi.state.seeking) {
        logMediaOverlay('handlePrevCommand ignored while seeking');
        return;
    }
    const platform = domUtils.detectPlatform();

    // If animation in progress, coalesce to a single pending "prev"
    if (navigationState.isAnimating) {
        navigationState.pending = 'prev';
        return;
    }

    if (navigationUtils.isHorizontalScrollAtStart()) {
        console.log("Reached start of current content, requesting previous page.");
        sendToNativeMessage({ action: 'prev' });
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
        textContent = textContent.replaceAll(/\s+/g, " ").trim();

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
    sendToNativeMessage({ action: 'characterposition', position: characterPosition });
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

    mediaOverlayUi.root = document.getElementById('mediaOverlayPlayerRoot');
    if (mediaOverlayUi.root) {
        mediaOverlayUi.root.dataset.visible = 'false';
        mediaOverlayUi.root.dataset.minimized = 'false';
        mediaOverlayUi.root.setAttribute('aria-live', 'polite');
    }

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

            requestMediaOverlayHighlightThemeRefresh();
            sendToNativeMessage({ action: 'pageload', value: true });
        } catch (error) {
            console.error("Error during iframe onload:", error);
        }
    };

    // Listen for messages from the parent window
    window.addEventListener("message", event => handleMessage(event, platform));
    if (platform.isIOS) {
        globalThis.addEventListener('touchstart', {});
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
    adjustVirtualColumns();


    // Adjust virtual columns after positioning content at the end
    // This ensures virtual columns are calculated with the correct final position
    setTimeout(() => {
        navigationUtils.scrollToPage(page);
        isPreviousPage = false;
        // Track the page we navigated to so persistence matches navigation
        const maxPage = getPageCount();
        const clampedPage = Math.min(page, maxPage);
        currentPage = clampedPage;
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

function setMediaOverlayTheme(theme) {
    const values = (theme && typeof theme === 'object') ? theme : null;
    const source = values ?? mediaOverlayThemeDefaults;

    mediaOverlayThemeState.highlightBackground = source.highlightBackground || mediaOverlayThemeDefaults.highlightBackground;
    mediaOverlayThemeState.highlightText = source.highlightText || mediaOverlayThemeDefaults.highlightText;
    mediaOverlayThemeState.highlightOutline = source.highlightOutline || mediaOverlayThemeDefaults.highlightOutline;

    requestMediaOverlayHighlightThemeRefresh();
}

/**
 * Return the 0-based index of `fragmentId` within the list of currently visible
 * segments on the active page, and the total count of visible segments.
 *
 * Usage from native: getVisibleSegmentPosition(fragmentId, ["f1","f2",...])
 * Returns: { index: number, count: number }
 */
function getVisibleSegmentPosition(fragmentId, segmentList) {
    try {
        const doc = domUtils.getIframeDocument();
        const win = domUtils.getContentWindow();
        if (!doc || !win) {
            return JSON.stringify({ index: -1, count: 0 });
        }

        let ids = [];
        if (Array.isArray(segmentList)) {
            ids = segmentList;
        } else if (segmentList) {
            ids = JSON.parse(segmentList);
        }
        const visible = [];
        for (const id of ids) {
            const el = doc.getElementById(id);
            if (!el) continue;
            const rect = el.getBoundingClientRect();
            // Visible if any part intersects the viewport horizontally
            if (rect.right >= 0 && rect.left <= win.innerWidth) {
                visible.push(id);
            }
        }

        const idx = visible.indexOf(fragmentId);
        return JSON.stringify({ index: idx, count: visible.length });
    } catch(e) {
        console.error('getVisibleSegmentPosition failed', e);
        return JSON.stringify({ index: -1, count: 0 });
    }
}

/**
 * Ensure the specified fragment is visible within the iframe viewport.
 * If the fragment lies beyond the current visible area, use the existing
 * pagination handlers so the host app can either scroll or ask native to
 * advance pages. This mirrors the expectation from the C# side which calls
 * this function before highlighting a fragment.
 */
function ensureFragmentVisibleUsingNext(fragmentId, direction) {
    try {
        const doc = domUtils.getIframeDocument();
        const win = domUtils.getContentWindow();
        if (!doc || !win || !fragmentId) {
            return;
        }

        const intent = direction === 'prev' ? 'prev' : 'next';

        const el = doc.getElementById(fragmentId);
        if (!el) {
            console.warn('ensureFragmentVisibleUsingNext: fragment not found', fragmentId);
            if (intent === 'prev') {
                handlePrevCommand();
            } else {
                handleNextCommand();
            }
            return;
        }

        const rect = el.getBoundingClientRect();
        // If any part of the element intersects the viewport horizontally it's visible
        const isVisible = rect.right >= 0 && rect.left <= win.innerWidth;
        if (isVisible) {
            return;
        }

        // If element is to the right of viewport, request next; if to the left, request prev
        if (rect.left > win.innerWidth) {
            // move right/next (this may call native when at end of content)
            handleNextCommand();
        } else if (rect.right < 0) {
            // move left/previous
            handlePrevCommand();
        } else if (intent === 'prev') {
            handlePrevCommand();
        } else {
            handleNextCommand();
        }
    } catch (e) {
        console.error('ensureFragmentVisibleUsingNext failed', e);
    }
}