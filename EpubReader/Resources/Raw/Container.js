/// <summary>
/// Container.js
/// This script handles navigation and interaction within an iframe for an ePub reader.
/// It detects the user's platform, manages link clicks, and handles navigation regions.
/// </summary>

// Constants
const TARGET_ORIGIN = "https://demo";
const TARGET_ORIGIN_MACIOS = "app://demo";

// Long press detection configuration
const longPressConfig = {
    pressTimeout: 800,     // Time in ms to consider a press as "long"
    moveTolerance: 10,     // Maximum movement in pixels allowed during press
};

// Long press state tracking
const longPressState = {
    startTime: 0,
    startX: 0,
    startY: 0,
    target: null,
    timeoutId: null,
    active: false,
};

/**
 * Detects the user's operating system platform
 * @returns {Object} Platform flags
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

// Helper function to send messages to parent
function sendMessageToParent(message) {
    if (detectPlatform().isIOS || detectPlatform().isMac) {
        window.parent.postMessage(message, TARGET_ORIGIN_MACIOS);
        console.log(`Sent message from ios/mac: ${message}`);
    }
    else {
        window.parent.postMessage(message, TARGET_ORIGIN);
        console.log(`Sent message: ${message}`);
    }
}

/**
 * Starts the long press timer
 * @param {Element} target - The target element
 * @param {number} x - Starting X coordinate
 * @param {number} y - Starting Y coordinate
 */
function startLongPressTimer(target, x, y) {
    clearLongPressTimer();

    longPressState.startTime = Date.now();
    longPressState.startX = x;
    longPressState.startY = y;
    longPressState.target = target;
    longPressState.active = true;

    longPressState.timeoutId = setTimeout(() => {
        handleLongPress(x, y);
    }, longPressConfig.pressTimeout);
}

/**
 * Clears the long press timer
 */
function clearLongPressTimer() {
    if (longPressState.timeoutId) {
        clearTimeout(longPressState.timeoutId);
        longPressState.timeoutId = null;
    }
    longPressState.active = false;
}

/**
 * Handles when a long press is detected
 * @param {number} x - X coordinate of the long press
 * @param {number} y - Y coordinate of the long press
 */
function handleLongPress(x, y) {
    if (!longPressState.active) return;
    
    console.log(`Long press detected at: ${x}, ${y}`);
    
    // Send the long press coordinates to parent (EpubText.js)
    sendMessageToParent(`longpress.${x},${y}`);
    
    // Clear the timer and state
    clearLongPressTimer();
}

// Handle link navigation
function handleLinkClick(href) {
    sendMessageToParent(`jump.${href}`);
    return true;
}

// Handle navigation regions - updated to work with both touch and click events
function handleNavigationClick(event) {
    // Get coordinates from either touch or click event
    let clickX;
    if (event.type === 'touchstart' || event.type === 'touchend') {
        // For touch events, get coordinates from the first touch point
        clickX = event.touches.length > 0 ? event.touches[0].clientX : event.changedTouches[0].clientX;
    } else {
        // For click events, use clientX directly
        clickX = event.clientX;
    }

    const pageWidth = window.innerWidth;
    const leftThreshold = pageWidth * 0.33;
    const rightThreshold = pageWidth * 0.66;

    if (clickX < leftThreshold) {
        console.log('Clicked in the left region');
        sendMessageToParent("prev");
    } else if (clickX > rightThreshold) {
        console.log('Clicked in the right region');
        sendMessageToParent("next");
    } else {
        console.log('Clicked in the center region');
        sendMessageToParent("menu");
    }
}

// Touch event handler with long press detection
document.addEventListener('touchstart', function (event) {
    console.log('touchstart', event.touches.length);

    if (event.touches.length !== 1) return; // Only handle single touches

    const touch = event.touches[0];
    const target = event.target;

    // Start long press detection
    startLongPressTimer(target, touch.clientX, touch.clientY);

    // Handle direct link click
    if (target.tagName === 'A') {
        return handleLinkClick(target.href);
    }

    // Handle click on link child element
    const parentLink = target.closest('a');
    if (parentLink) {
        return handleLinkClick(parentLink.href);
    }

    // Don't handle navigation click yet - wait to see if it's a long press
});

// Track touch movement to cancel long press if moved too much
document.addEventListener('touchmove', function (event) {
    if (!longPressState.active || event.touches.length !== 1) return;

    const touch = event.touches[0];

    // Check if touch has moved beyond tolerance
    const moveX = Math.abs(touch.clientX - longPressState.startX);
    const moveY = Math.abs(touch.clientY - longPressState.startY);

    if (moveX > longPressConfig.moveTolerance || moveY > longPressConfig.moveTolerance) {
        // Cancel long press if moved beyond tolerance
        clearLongPressTimer();
    }
});

// Touch end handler - if not a long press, handle as navigation
document.addEventListener('touchend', function (event) {
    // If long press wasn't triggered and timer was active, it was a short press
    if (longPressState.active) {
        clearLongPressTimer();
        // Only handle navigation if it wasn't a long press
        handleNavigationClick(event);
    }
    
    // Prevent the delayed click event on mobile
    if (event.target.tagName !== 'A' && !event.target.closest('a')) {
        event.preventDefault();
    }
});

// Cancel long press on touch cancel
document.addEventListener('touchcancel', function () {
    clearLongPressTimer();
});

// Click event handler for non-touch devices
window.addEventListener('click', function (event) {
    // Skip if this is a touch device to avoid double events
    if ('ontouchstart' in window) {
        console.log('iframe window event listener clicked');
        return;
    }
    if (detectPlatform().isIOS || detectPlatform().isMac) {
        console.log('iframe window event listener clicked');
        return;
    }
    const target = event.target;

    // Handle direct link click
    if (target.tagName === 'A') {
        return handleLinkClick(target.href);
    }

    // Handle click on link child element
    const parentLink = target.closest('a');
    if (parentLink) {
        return handleLinkClick(parentLink.href);
    }
    event.preventDefault();
    // Handle navigation click
    handleNavigationClick(event);
});

// Keyboard navigation handler
window.addEventListener("keydown", function (event) {
    if (event.key === "ArrowRight") {
        sendMessageToParent("next");
    } else if (event.key === "ArrowLeft") {
        sendMessageToParent("prev");
    }
});