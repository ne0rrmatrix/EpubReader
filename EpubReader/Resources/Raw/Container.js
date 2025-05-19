window.addEventListener('click', function (event) {
    let target = event.target;

    if (target.tagName === 'A') {
        // Click was directly on a link
        console.log('Clicked on link:', target.href);
        window.parent.postMessage("jump." + target.href, "https://demo");
        return;
    } else if (target.closest('a')) {
        // Click was on a child of a link
        let link = target.closest('a');
        window.parent.postMessage("jump." + link.href, "https://demo");
        console.log('Clicked on link:', link.href);
        return;
    }

    event.preventDefault();
    // Get the x-coordinate of the click relative to the viewport
    const clickX = event.clientX;

    // Get the total width of the viewport
    const pageWidth = window.innerWidth;

    // Calculate the boundaries for the three regions
    const leftThreshold = pageWidth * 0.33;
    const rightThreshold = pageWidth * 0.66;

    // Determine which region the click occurred in and perform an action
    if (clickX < leftThreshold) {
        // Action for the left 33 percent
        console.log('Clicked in the right region');
        window.parent.postMessage("prev", "https://demo");
    } else if (clickX > rightThreshold) {
        // Action for the right 33 percent
        console.log('Clicked in the left region');
        window.parent.postMessage("next", "https://demo");
    } else {
        // Action for the center region (the remaining area)
        console.log('Clicked in the center region');
        window.parent.postMessage("menu", "https://demo");
    }
});

window.addEventListener("keydown", function (e) {
    if (e.key == "ArrowRight") {
        console.log("ArrowRight sent to parent.");
        window.parent.postMessage("next", "https://demo");
    } else if (e.key == "ArrowLeft") {
        console.log("ArrowLeft sent to parent.");
        window.parent.postMessage("prev", "https://demo");
    }
});