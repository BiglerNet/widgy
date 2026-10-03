# Card and clip paint cost (theme-and-card)

Measured while implementing the `theme-and-card` change: one 1100x550 widget surface (a 4x2 Clock cell at 1100 px
wide), software raster, default-dark theme, mean of 300 paints after warm-up, Release build, `SKBitmap` target.

| Path | Mean per paint |
|---|---|
| Clock `Render` alone (no card) | 0.144 ms |
| Rounded card fill + Clock `Render` (no clip) | 0.198 ms |
| `WidgetPainter.Paint`: card fill + antialiased rounded clip + Clock `Render` | 0.282 ms |

The antialiased rounded clip adds about 0.08 ms per paint, and the card fill about 0.05 ms. That is far below the
1 ms threshold set in the design, so the clip stays unconditional. Widgets repaint at most once a minute (Clock) so the
absolute cost is negligible for idle CPU.
