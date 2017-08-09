# CanvasTest
This project is for Minimal, Complete, and Verifiable example to this problem: https://stackoverflow.com/questions/45570115/wpf-canvas-stops-redraw-after-scaletransform

How to reproduce the bug:
1. Start application.
2. Try some mouse dragging to draw a block. - The selection is visible.
3. Zoom out to for example 40% by slider in right bottom corner.
4. Try some mouse dragging to draw a block. - The selection is now NOT visible.
5. Zoom back to 100%, it gets work again.
