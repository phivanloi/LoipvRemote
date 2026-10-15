"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const {
  calculateTerminalFontSize
} = require("../../LoipvRemote.WinUI/TerminalAssets/terminal-display-scaling.js");

test("uses the SSH-balanced base size at a common terminal viewport", () => {
  assert.equal(calculateTerminalFontSize(1280, 720, 1), 14);
});

test("scales within readable limits as the available viewport changes", () => {
  assert.equal(calculateTerminalFontSize(800, 600, 1), 13.5);
  assert.equal(calculateTerminalFontSize(1920, 1080, 1), 14.5);
  assert.equal(calculateTerminalFontSize(3840, 2160, 1), 16);
});

test("does not multiply CSS font size by DPI because WebView already rasterizes CSS pixels", () => {
  assert.equal(
    calculateTerminalFontSize(1920, 1080, 2),
    calculateTerminalFontSize(1920, 1080, 1)
  );
});

test("falls back safely while the renderer has not been measured", () => {
  assert.equal(calculateTerminalFontSize(0, 0, 1), 14);
  assert.equal(calculateTerminalFontSize(Number.NaN, Number.NaN, 1), 14);
});
