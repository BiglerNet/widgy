## MODIFIED Requirements

### Requirement: WidgetRenderContext
Each widget MUST receive a `WidgetRenderContext` during `Render` containing:

- `SKCanvas Canvas` — SkiaSharp canvas for 2D drawing, sized to the widget's card, already showing the host-drawn card
  and clipped to its shape
- `DateTime Time` — Current timestamp at render time
- `Size PixelSize` — The card's pixel dimensions (width, height); the widget's drawing area starts at `(0, 0)`
- `Theme Theme` — The active theme, with every size already in pixels for this surface (see the `theme` capability)
- `SKRect ContentRect` — The card's area inset by the theme's padding; where content normally goes
- `WidgetConfig Config` — The widget's configuration (concrete type)
- `CancellationToken CancellationToken` — For cooperative cancellation

The colour-only `ThemeColors` type no longer exists. This is a deliberate breaking change to the SDK: the SDK assembly
version changes with it, and widgets built against the earlier SDK must be rebuilt.

#### Scenario: Context is populated at render time
- **WHEN** the runtime calls `Render()`
- **THEN** `WidgetRenderContext` is fully populated with current time, card pixel size, theme, content rectangle and
  config

#### Scenario: Content rectangle follows the theme
- **WHEN** the theme's padding resolves to 20 pixels and the card is 550 by 275 pixels
- **THEN** `ContentRect` is the rectangle from `(20, 20)` to `(530, 255)`
