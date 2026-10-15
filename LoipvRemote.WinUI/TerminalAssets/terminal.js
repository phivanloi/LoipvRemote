window.addEventListener("error", event => {
  window.chrome.webview.postMessage({
    kind: "error",
    message: event.message || "The terminal renderer failed to load."
  });
});

window.addEventListener("unhandledrejection", event => {
  window.chrome.webview.postMessage({
    kind: "error",
    message: String(event.reason || "The terminal renderer rejected an operation.")
  });
});

(() => {
  "use strict";

  const displayScaling = window.LoipvRemoteTerminalDisplayScaling;
  const initialFontSize = displayScaling.calculateTerminalFontSize(
    window.innerWidth,
    window.innerHeight,
    window.devicePixelRatio);
  const terminal = new Terminal({
    allowProposedApi: false,
    convertEol: false,
    cursorBlink: true,
    cursorStyle: "block",
    fontFamily: '"Cascadia Mono", "Cascadia Code", Consolas, monospace',
    fontSize: initialFontSize,
    lineHeight: 1.05,
    minimumContrastRatio: 4.5,
    rightClickSelectsWord: false,
    screenReaderMode: false,
    scrollback: 5000,
    theme: {
      background: "#0c0c0c",
      foreground: "#cccccc",
      cursor: "#f2f2f2",
      selectionBackground: "#264f78"
    }
  });
  const fitAddon = new FitAddon.FitAddon();
  terminal.loadAddon(fitAddon);
  terminal.open(document.getElementById("terminal"));

  let resizeTimer;
  let lastColumns = 0;
  let lastRows = 0;
  let resolutionQuery;

  function post(message) {
    window.chrome.webview.postMessage(message);
  }

  function updateFontSize() {
    const fontSize = displayScaling.calculateTerminalFontSize(
      window.innerWidth,
      window.innerHeight,
      window.devicePixelRatio);
    if (terminal.options.fontSize === fontSize) {
      return;
    }

    terminal.options.fontSize = fontSize;
    terminal.clearTextureAtlas();
  }

  function fitAndReport() {
    updateFontSize();
    fitAddon.fit();
    if (terminal.cols === lastColumns && terminal.rows === lastRows) {
      return;
    }

    lastColumns = terminal.cols;
    lastRows = terminal.rows;
    post({ kind: "resize", columns: terminal.cols, rows: terminal.rows });
  }

  function scheduleFit() {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(fitAndReport, 40);
  }

  function watchCurrentDpi() {
    if (resolutionQuery) {
      if (typeof resolutionQuery.removeEventListener === "function") {
        resolutionQuery.removeEventListener("change", onDpiChanged);
      } else {
        resolutionQuery.removeListener(onDpiChanged);
      }
    }

    resolutionQuery = window.matchMedia(
      `(resolution: ${window.devicePixelRatio}dppx)`);
    if (typeof resolutionQuery.addEventListener === "function") {
      resolutionQuery.addEventListener("change", onDpiChanged, { once: true });
    } else {
      resolutionQuery.addListener(onDpiChanged);
    }
  }

  function onDpiChanged() {
    watchCurrentDpi();
    scheduleFit();
  }

  terminal.onData(data => post({ kind: "input", data }));
  terminal.attachCustomKeyEventHandler(event => {
    if (event.type !== "keydown" || !event.ctrlKey || !event.shiftKey) {
      return true;
    }

    if (event.code === "KeyC" && terminal.hasSelection()) {
      navigator.clipboard.writeText(terminal.getSelection());
      return false;
    }

    if (event.code === "KeyV") {
      navigator.clipboard.readText().then(text => {
        if (text) {
          post({ kind: "input", data: text });
        }
      });
      return false;
    }

    return true;
  });

  window.chrome.webview.addEventListener("message", event => {
    const message = event.data;
    if (!message || typeof message.kind !== "string") {
      return;
    }

    if (message.kind === "output" && typeof message.data === "string") {
      terminal.write(message.data);
    } else if (message.kind === "focus") {
      terminal.focus();
    } else if (message.kind === "clear") {
      terminal.clear();
    }
  });

  window.addEventListener("resize", scheduleFit);
  new ResizeObserver(scheduleFit).observe(document.body);
  watchCurrentDpi();
  document.addEventListener("pointerdown", () => terminal.focus());
  document.addEventListener("visibilitychange", () => {
    if (!document.hidden) {
      scheduleFit();
      terminal.focus();
    }
  });

  requestAnimationFrame(() => {
    fitAndReport();
    terminal.focus();
    post({ kind: "ready", columns: terminal.cols, rows: terminal.rows });
  });
})();
