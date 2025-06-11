// Constants
const TARGET_ORIGIN = "https://demo";

// Helper function to send messages to parent
function sendMessageToParent(message) {
    window.parent.postMessage(message, TARGET_ORIGIN);
    console.log(`Sent message: ${message}`);
}

// Handle link navigation
function handleLinkClick(href) {
    sendMessageToParent(`jump.${href}`);
    return true;
}
// Handle touch regions
function handleTouchRegion(event) {
    event.preventDefault();
    const touchX = event.touches[0].clientX;
    const pageWidth = window.innerWidth;
    const leftThreshold = pageWidth * 0.33;
    const rightThreshold = pageWidth * 0.66;
    if (touchX < leftThreshold) {
        console.log('Touched in the left region');
        sendMessageToParent("prev");
    } else if (touchX > rightThreshold) {
        console.log('Touched in the right region');
        sendMessageToParent("next");
    } else {
        console.log('Touched in the center region');
        sendMessageToParent("menu");
    }
}
// Handle navigation regions
function handleNavigationClick(event) {
    event.preventDefault();

    const clickX = event.clientX;
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

// Click event handler
window.addEventListener('click', function (event) {
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
// Touch event handler
window.addEventListener('touchstart', function (event) {
    const target = event.target;
    // Handle direct link touch
    if (target.tagName === 'A') {
        return handleLinkClick(target.href);
    }
    // Handle touch on link child element
    const parentLink = target.closest('a');
    if (parentLink) {
        return handleLinkClick(parentLink.href);
    }
    // Handle navigation touch
    handleTouchRegion(event);
});
// Keyboard navigation handler
window.addEventListener("keydown", function (event) {
    if (event.key === "ArrowRight") {
        sendMessageToParent("next");
    } else if (event.key === "ArrowLeft") {
        sendMessageToParent("prev");
    }
});