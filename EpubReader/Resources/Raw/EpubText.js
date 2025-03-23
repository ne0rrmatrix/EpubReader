document.addEventListener("DOMContentLoaded", function() {
  const frame = document.getElementById("page");

  const scrollLeft = () => {
    const gap = parseInt(window.getComputedStyle(frame.contentWindow.document.documentElement).getPropertyValue("column-gap"));
    frame.contentWindow.scrollTo(frame.contentWindow.scrollX - frame.contentWindow.innerWidth - gap, 0);
  };

  const scrollRight = () => {
    const gap = parseInt(window.getComputedStyle(frame.contentWindow.document.documentElement).getPropertyValue("column-gap"));
    frame.contentWindow.scrollTo(frame.contentWindow.scrollX + frame.contentWindow.innerWidth + gap, 0);
  };

  document.body.addEventListener("click",async function(e) {
    e.preventDefault();
    if (e.clientX > (window.innerWidth / 2)) {
      if(isHorizontallyScrolledToEnd()) {
         await window.HybridWebView.InvokeDotNet('DoSyncWork');
        return;
      }
      scrollRight();
    } else {
      if(isHorizontalScrollAtStart()) {
           await window.HybridWebView.InvokeDotNet('DoSomeWork1');
        return;
      }
      scrollLeft();
    }
  });

  document.body.addEventListener("keydown",async function(e) {
    if (e.key == "ArrowRight") {
      if(isHorizontallyScrolledToEnd()) {
         await window.HybridWebView.InvokeDotNet('DoSyncWork');
        return;
      }
      scrollRight();
    } else if (e.key == "ArrowLeft") {
      if(isHorizontalScrollAtStart()) {
           await window.HybridWebView.InvokeDotNet('DoSomeWork1');
        return;
      }
      scrollLeft();
    }
  });
});

function isHorizontallyScrolledToEnd() {
  const frame = document.getElementById("page");
  if (!frame.contentWindow) {
    return false;
  }
  console.log("isHorizontallyScrolledToEnd");
  const contentDoc = frame.contentWindow.document.documentElement;
  const maxScrollLeft = contentDoc.scrollWidth - contentDoc.clientWidth;
  return Math.abs(frame.contentWindow.scrollX - maxScrollLeft) <= 10;
}

function isHorizontalScrollAtStart() {
  const frame = document.getElementById("page");
  if (!frame.contentWindow) {
    console.log("frame.contentWindow is null");
    return false;
  }
    console.log(frame.contentWindow.scrollX);
  return frame.contentWindow.scrollX <= 0;
}
function scrollToHorizontalEnd() {
  const frame = document.getElementById("page");
  if (frame?.contentWindow && frame.contentWindow.document.readyState === 'complete') {
    const contentDoc = frame.contentDocument || frame.contentWindow.document;
    const maxScrollLeft = contentDoc.documentElement.scrollWidth;
    console.log(maxScrollLeft);
    frame.contentWindow.scrollTo(maxScrollLeft, 0);
    return true;
  } else if (frame) {
    // Iframe might not be loaded yet, wait for the 'load' event
    frame.onload = function() {
      scrollToHorizontalEnd(); // Call the function again when loaded
      frame.onload = null; // Remove the event listener after it's executed once
    };
  } else {
    console.error("Iframe element not provided.");
}
}
function setIframeSource(url) {
        const frame = document.getElementById("page");
        frame.src = url;
        console.log("Setting iframe source to: " + url);
}