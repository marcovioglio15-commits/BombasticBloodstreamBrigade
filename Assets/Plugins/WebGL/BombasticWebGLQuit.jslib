mergeInto(LibraryManager.library, {
  BombasticWebGLQuit: function () {
    var existingNotice = document.getElementById("bombastic-webgl-quit-notice");

    if (existingNotice) {
      return;
    }

    var canvas = Module.canvas;
    var container = document.getElementById("unity-container");

    if (!container && canvas) {
      container = canvas.parentElement;
    }

    if (!container) {
      container = document.body;
    }

    if (canvas) {
      canvas.style.display = "none";
    }

    var notice = document.createElement("div");
    notice.id = "bombastic-webgl-quit-notice";
    notice.style.alignItems = "center";
    notice.style.background = "#140f17";
    notice.style.boxSizing = "border-box";
    notice.style.color = "#ffffff";
    notice.style.display = "flex";
    notice.style.flexDirection = "column";
    notice.style.fontFamily = "Arial, sans-serif";
    notice.style.gap = "16px";
    notice.style.height = "100%";
    notice.style.justifyContent = "center";
    notice.style.left = "0";
    notice.style.padding = "32px";
    notice.style.position = "absolute";
    notice.style.textAlign = "center";
    notice.style.top = "0";
    notice.style.width = "100%";
    notice.style.zIndex = "1000";

    var title = document.createElement("strong");
    title.textContent = "Session ended";
    title.style.fontSize = "28px";

    var message = document.createElement("span");
    message.textContent = "Close this tab, or reload the page to play again.";
    message.style.fontSize = "16px";

    notice.appendChild(title);
    notice.appendChild(message);
    container.appendChild(notice);
  }
});
