window.addEventListener("click", function (e) {
   
    e.preventDefault();
   
    if (e.clientX > (window.innerWidth / 2)) {
        console.log("clicking next sent to parent.");
        if(iOS())
        {
            window.parent.postMessage("next", "app://demo");
            return;
        }
        window.parent.postMessage("next", "https://demo");
    } else {
        console.log("clicking prev sent to parent.");
        if (iOS())
        {
            window.parent.postMessage("prev", "app://demo");
        }
        window.parent.postMessage("prev", "https://demo");
    }
});

window.addEventListener("keydown", function (e) {
    if (e.key === "ArrowRight") {
        console.log("ArrowRight sent to parent.");
        if(iOS())
        {
            window.parent.postMessage("next", "app://demo");
            return;
        }
        window.parent.postMessage("next", "https://demo");
    } else if (e.key === "ArrowLeft") {
        console.log("ArrowLeft sent to parent.");
        if(iOS())
        {
            window.parent.postMessage("prev", "app://demo");
            return;
        }
        window.parent.postMessage("prev", "https://demo");
    }
});

function iOS() {
    return [
            'iPad Simulator',
            'iPhone Simulator',
            'iPod Simulator',
            'iPad',
            'iPhone',
            'iPod'
        ].includes(navigator.platform)
        // iPad on iOS 13 detection
        || (navigator.userAgent.includes("Mac") && "ontouchend" in document)
}