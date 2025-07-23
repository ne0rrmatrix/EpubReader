/// <summary>
/// Container.js
/// This script handles navigation and interaction within an iframe for an ePub reader.
/// It detects the user's platform, manages link clicks, and handles navigation regions.
/// </summary>

// Constants
const TARGET_ORIGIN = "https://demo";
const TARGET_ORIGIN_MACIOS = "app://demo";

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

// Touch event handler
document.addEventListener('touchstart', function (event) {
    console.log('touchstart', event.touches.length);

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

    // Handle navigation click
    handleNavigationClick(event);
});

// Optional: Add touchend handler for better touch experience
document.addEventListener('touchend', function (event) {
    // Prevent the delayed click event on mobile
    if (event.target.tagName !== 'A' && !event.target.closest('a')) {
        event.preventDefault();
    }
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