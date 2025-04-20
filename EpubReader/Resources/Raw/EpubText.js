document.addEventListener("DOMContentLoaded", function () {
    const frame = document.getElementById("page");
    const body = document.getElementById("body");
    const root = document.documentElement;
    const width = Math.floor(frame.contentWindow.innerWidth);
    const height = Math.floor(frame.contentWindow.innerHeight);
    frame.style.width = width + "px";
    body.style.width = width + "px";
    body.style.height = height + "px";
    frame.style.height = height + "px";
    root.style.setProperty('--root-width', width + "px");
    root.style.setProperty('--root-height', height + "px");

    function isHorizontalScrollAtStart() {
        const frame = document.getElementById("page");
        if (!frame.contentWindow) {
            console.log("frame.contentWindow is null");
            return false;
        }
        return frame.contentWindow.scrollX <= 0;
    }

    function isHorizontallyScrolledToEnd() {
        const frame = document.getElementById("page");
        if (!frame.contentWindow) {
            return false;
        }
        console.log("isHorizontallyScrolledToEnd");
        const contentDoc = frame.contentWindow.document.documentElement;
        const maxScrollLeft = contentDoc.scrollWidth - contentDoc.clientWidth;
        return Math.abs(frame.contentWindow.scrollX - maxScrollLeft) <= 30;
    }

    const scrollLeft = () => {
        const gap = parseInt(window.getComputedStyle(frame.contentWindow.document.documentElement).getPropertyValue("column-gap"));
        frame.contentWindow.scrollTo(frame.contentWindow.scrollX - frame.contentWindow.innerWidth - gap, 0);
    };


    const scrollRight = () => {
        const gap = parseInt(window.getComputedStyle(frame.contentWindow.document.documentElement).getPropertyValue("column-gap"));
        console.log(frame.contentWindow.scrollX + frame.contentWindow.innerWidth + gap);
        console.log(frame.contentWindow.scrollX);
        console.log(frame.contentWindow.innerWidth);
        frame.contentWindow.scrollTo(frame.contentWindow.scrollX + frame.contentWindow.innerWidth + gap, 0);
    };

    window.addEventListener("message", function (event) {
        if (event.origin !== "https://demo") {
            return;
        }
        if (event.data === "next") {
            if (isHorizontallyScrolledToEnd()) {
                console.log("received scrollRight");
                window.location.href = 'https://runcsharp.next?true';
                return;
            }
            scrollRight();
        } else if (event.data === "prev") {
            if (isHorizontalScrollAtStart()) {
                console.log("received scrollLeft");
                window.location.href = 'https://runcsharp.prev?true';
                return;
            }
            scrollLeft();
        }
    });
});

let isPreviousPage = false;
let iframe = document.getElementById("page");

function gotoEnd () {
    if (isPreviousPage) {
        scrollToHorizontalEnd();
        isPreviousPage = false;
    }
};
function setPreviousPage() {
    isPreviousPage = true;
}

function loadPage(page) {
    let frame = document.getElementById("page");
    if (frame == null) {
        console.log("Frame not found");
        return false;
    }
    console.log("Frame found. Loading page:", page);
    frame.setAttribute('src', page); // Fixed: 'page' was in quotes, should be the variable
    return true;
}
function scrollToHorizontalEnd() {
    const frame = document.getElementById("page");
    if (frame?.contentWindow && frame.contentWindow.document.readyState === 'complete') {
        const contentDoc = frame.contentDocument || frame.contentWindow.document;
        const maxScrollLeft = contentDoc.documentElement.scrollWidth;
        console.log("Scrolling to end of container.");
        frame.contentWindow.scrollTo(maxScrollLeft, 0);
        return true;
    } else if (frame) {
        // Iframe might not be loaded yet, wait for the 'load' event
        frame.onload = function () {
            scrollToHorizontalEnd(); // Call the function again when loaded
            frame.onload = null; // Remove the event listener after it's executed once
        };
    } else {
        console.error("Iframe element not provided.");
    }
}
function setReadiumProperty(property, value) {
        let root = document.getElementById("page").contentWindow.document.documentElement;
        console.log(property, value);
        root.style.setProperty(property, value);
}

function UnsetReadiumProperty(property) {
        let root = document.getElementById("page").contentWindow.document.documentElement;
        root.style.removeProperty(property);
}

function setBackgroundColor(color) {
        document.documentElement.style.setProperty('--background-color', color);
}
function UnsetBackgroundColor() {
    document.documentElement.style.removeProperty('--background-color');
}

document.getElementById("page").addEventListener("load", function () {
    window.location.href = 'https://runcsharp.pageLoad?true';
});