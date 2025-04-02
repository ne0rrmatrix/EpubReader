﻿window.addEventListener("click", function (e) {
    e.preventDefault();
    if (e.clientX > (window.innerWidth / 2)) {
        console.log("clicking next sent to parent.");
        window.parent.postMessage("next", "*");
    } else {
        console.log("clicking prev sent to parent.");
        window.parent.postMessage("prev", "*");
    }
});

window.addEventListener("keydown", function (e) {
    if (e.key == "ArrowRight") {
        console.log("ArrowRight sent to parent.");
        window.parent.postMessage("next", "*");
    } else if (e.key == "ArrowLeft") {
        console.log("ArrowLeft sent to parent.");
        window.parent.postMessage("prev", "*");
    }
});