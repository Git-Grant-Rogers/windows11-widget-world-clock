#!/usr/bin/env python3
"""
Generates the placeholder PNG assets for the MSIX package and the widget picker.

Pure Python (no Pillow) so it runs anywhere. Shapes are drawn with signed-distance
functions and 1px anti-aliasing. Colours follow the Windows 11 Fluent palette:
accent #0067C0, light surface #F9F9F9, dark surface #2B2B2B.

Run from the repository root:  python3 tools/generate-assets.py
Replace the widget screenshots with real captures once the widget is running.
"""
from __future__ import annotations

import math
import os
import struct
import zlib

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "WorldClockWidget")
ASSETS = os.path.join(ROOT, "Assets")
PROVIDER_ASSETS = os.path.join(ROOT, "ProviderAssets")

ACCENT = (0x00, 0x67, 0xC0)
ACCENT_DARK = (0x00, 0x4C, 0x8F)
WHITE = (0xFF, 0xFF, 0xFF)
LIGHT_SURFACE = (0xF9, 0xF9, 0xF9)
LIGHT_STROKE = (0xE5, 0xE5, 0xE5)
LIGHT_TEXT = (0x1B, 0x1B, 0x1B)
LIGHT_SUBTLE = (0x9A, 0x9A, 0x9A)
DARK_SURFACE = (0x2B, 0x2B, 0x2B)
DARK_STROKE = (0x3D, 0x3D, 0x3D)
DARK_TEXT = (0xFF, 0xFF, 0xFF)
DARK_SUBTLE = (0x9E, 0x9E, 0x9E)


class Canvas:
    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height
        # Straight (non-premultiplied) RGBA floats.
        self.px = [[0.0, 0.0, 0.0, 0.0] for _ in range(width * height)]

    def _blend(self, x: int, y: int, color: tuple[int, int, int], coverage: float) -> None:
        if coverage <= 0.0:
            return
        coverage = min(1.0, coverage)
        p = self.px[y * self.width + x]
        sa = coverage
        da = p[3]
        oa = sa + da * (1.0 - sa)
        if oa <= 0.0:
            return
        for i in range(3):
            p[i] = (color[i] * sa + p[i] * da * (1.0 - sa)) / oa
        p[3] = oa

    def _paint(self, x0: float, y0: float, x1: float, y1: float, sdf, color) -> None:
        xs, ys = max(0, int(math.floor(x0)) - 1), max(0, int(math.floor(y0)) - 1)
        xe, ye = min(self.width, int(math.ceil(x1)) + 2), min(self.height, int(math.ceil(y1)) + 2)
        for y in range(ys, ye):
            cy = y + 0.5
            for x in range(xs, xe):
                d = sdf(x + 0.5, cy)
                self._blend(x, y, color, 0.5 - d)

    def rounded_rect(self, x, y, w, h, r, color) -> None:
        cx, cy, hw, hh = x + w / 2.0, y + h / 2.0, w / 2.0, h / 2.0

        def sdf(px, py):
            qx = abs(px - cx) - hw + r
            qy = abs(py - cy) - hh + r
            outside = math.hypot(max(qx, 0.0), max(qy, 0.0))
            inside = min(max(qx, qy), 0.0)
            return outside + inside - r

        self._paint(x, y, x + w, y + h, sdf, color)

    def circle(self, cx, cy, r, color) -> None:
        self._paint(cx - r, cy - r, cx + r, cy + r, lambda px, py: math.hypot(px - cx, py - cy) - r, color)

    def ring(self, cx, cy, r, thickness, color) -> None:
        half = thickness / 2.0
        self._paint(cx - r - half, cy - r - half, cx + r + half, cy + r + half,
                    lambda px, py: abs(math.hypot(px - cx, py - cy) - r) - half, color)

    def line(self, x0, y0, x1, y1, width, color) -> None:
        half = width / 2.0
        dx, dy = x1 - x0, y1 - y0
        length_sq = dx * dx + dy * dy or 1.0

        def sdf(px, py):
            t = max(0.0, min(1.0, ((px - x0) * dx + (py - y0) * dy) / length_sq))
            return math.hypot(px - (x0 + t * dx), py - (y0 + t * dy)) - half

        self._paint(min(x0, x1) - half, min(y0, y1) - half, max(x0, x1) + half, max(y0, y1) + half, sdf, color)

    def save(self, path: str) -> None:
        raw = bytearray()
        for y in range(self.height):
            raw.append(0)  # filter type: none
            row = self.px[y * self.width:(y + 1) * self.width]
            for p in row:
                a = p[3]
                raw += bytes((int(round(p[0])), int(round(p[1])), int(round(p[2])), int(round(a * 255))))

        def chunk(tag: bytes, data: bytes) -> bytes:
            body = tag + data
            return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

        png = b"\x89PNG\r\n\x1a\n"
        png += chunk(b"IHDR", struct.pack(">IIBBBBB", self.width, self.height, 8, 6, 0, 0, 0))
        png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        png += chunk(b"IEND", b"")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as handle:
            handle.write(png)
        print(f"wrote {os.path.relpath(path)} ({self.width}x{self.height})")


def draw_clock_glyph(canvas: Canvas, cx: float, cy: float, radius: float, face=ACCENT, hands=WHITE) -> None:
    """A round accent-coloured clock face reading 10:10, the classic 'friendly' watch pose."""
    canvas.circle(cx, cy, radius, face)
    canvas.ring(cx, cy, radius * 0.78, max(1.5, radius * 0.07), hands)
    hand = max(1.5, radius * 0.09)
    # Hour hand towards 10 o'clock, minute hand towards 2 o'clock.
    canvas.line(cx, cy, cx + radius * 0.42 * math.cos(math.radians(-150)), cy + radius * 0.42 * math.sin(math.radians(-150)), hand, hands)
    canvas.line(cx, cy, cx + radius * 0.6 * math.cos(math.radians(-30)), cy + radius * 0.6 * math.sin(math.radians(-30)), hand, hands)
    canvas.circle(cx, cy, hand * 0.9, hands)


def logo(size: int, path: str, padding_ratio: float = 0.12) -> None:
    canvas = Canvas(size, size)
    pad = size * padding_ratio
    draw_clock_glyph(canvas, size / 2.0, size / 2.0, size / 2.0 - pad)
    canvas.save(path)


def wide_logo(width: int, height: int, path: str) -> None:
    canvas = Canvas(width, height)
    radius = height * 0.36
    draw_clock_glyph(canvas, width * 0.3, height / 2.0, radius)
    # Three "time" bars stand in for city rows.
    bar_x = width * 0.48
    for i, frac in enumerate((0.34, 0.26, 0.3)):
        y = height * (0.32 + i * 0.18)
        canvas.rounded_rect(bar_x, y, width * frac, height * 0.08, height * 0.04, ACCENT)
    canvas.save(path)


def screenshot(path: str, dark: bool) -> None:
    """300x304 preview of the medium widget for the picker, per the Windows design guidance."""
    width, height = 300, 304
    surface = DARK_SURFACE if dark else LIGHT_SURFACE
    stroke = DARK_STROKE if dark else LIGHT_STROKE
    text = DARK_TEXT if dark else LIGHT_TEXT
    subtle = DARK_SUBTLE if dark else LIGHT_SUBTLE

    canvas = Canvas(width, height)
    canvas.rounded_rect(0, 0, width, height, 8, stroke)
    canvas.rounded_rect(1, 1, width - 2, height - 2, 7, surface)

    # Attribution area (48px): icon + title bar.
    draw_clock_glyph(canvas, 16 + 10, 24, 10)
    canvas.rounded_rect(16 + 28, 18, 84, 12, 6, text)

    # Four clock rows: label + caption on the left, time on the right.
    top = 48 + 16
    for i, (label_w, time_w) in enumerate(((58, 62), (82, 54), (50, 62), (66, 54))):
        y = top + i * 56
        canvas.rounded_rect(16, y, label_w, 12, 6, text)
        canvas.rounded_rect(16, y + 20, label_w + 40, 8, 4, subtle)
        canvas.rounded_rect(width - 16 - time_w, y + 2, time_w, 18, 9, text)

    canvas.save(path)


def main() -> None:
    logo(50, os.path.join(ASSETS, "StoreLogo.png"))
    logo(100, os.path.join(ASSETS, "StoreLogo.scale-200.png"))
    logo(300, os.path.join(ASSETS, "Square150x150Logo.scale-200.png"))
    logo(88, os.path.join(ASSETS, "Square44x44Logo.scale-200.png"))
    logo(24, os.path.join(ASSETS, "Square44x44Logo.targetsize-24_altform-unplated.png"), padding_ratio=0.04)
    logo(48, os.path.join(ASSETS, "LockScreenLogo.scale-200.png"))
    wide_logo(620, 300, os.path.join(ASSETS, "Wide310x150Logo.scale-200.png"))
    wide_logo(1240, 600, os.path.join(ASSETS, "SplashScreen.scale-200.png"))

    logo(64, os.path.join(PROVIDER_ASSETS, "WorldClock_Icon.png"), padding_ratio=0.06)
    screenshot(os.path.join(PROVIDER_ASSETS, "WorldClock_Screenshot.png"), dark=False)
    screenshot(os.path.join(PROVIDER_ASSETS, "WorldClock_Screenshot_Light.png"), dark=False)
    screenshot(os.path.join(PROVIDER_ASSETS, "WorldClock_Screenshot_Dark.png"), dark=True)


if __name__ == "__main__":
    main()
