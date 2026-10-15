(function (root, factory) {
  "use strict";

  const api = factory();
  if (typeof module === "object" && module.exports) {
    module.exports = api;
  }

  root.LoipvRemoteTerminalDisplayScaling = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";

  const baseFontSize = 14;
  const minimumFontSize = 13.5;
  const maximumFontSize = 16;
  const referenceWidth = 1280;
  const referenceHeight = 720;

  function roundToHalf(value) {
    return Math.round(value * 2) / 2;
  }

  function calculateTerminalFontSize(width, height, devicePixelRatio) {
    if (!Number.isFinite(width) || width <= 0 ||
        !Number.isFinite(height) || height <= 0) {
      return baseFontSize;
    }

    // WebView2 CSS pixels are already DPI-aware. devicePixelRatio is accepted
    // so a DPI transition can trigger recalculation, but multiplying by it
    // would scale the terminal twice on a high-DPI monitor.
    void devicePixelRatio;
    const viewportScale = Math.sqrt(
      (width * height) / (referenceWidth * referenceHeight));
    const scaledSize = baseFontSize + (Math.log2(viewportScale) * 1.2);
    return Math.min(
      maximumFontSize,
      Math.max(minimumFontSize, roundToHalf(scaledSize)));
  }

  return Object.freeze({
    calculateTerminalFontSize
  });
});
