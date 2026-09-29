# Clock Widget Specification

## ADDED Requirements

### Requirement: Clock Display
The Clock widget (`typeId` `urdeck.widgets.clock`) MUST display the current time and date on its SkiaSharp surface:

- Primary display: `HH:mm` (24-hour clock by default, configurable to 12-hour `h:mm tt`)
- Secondary display: Date line, formatted `ddd MMM d, yyyy`
- Text drawn with SkiaSharp antialiased, subpixel-positioned `SKFont`, centered horizontally
- Color: the config `TextColor` override if valid, otherwise `WidgetRenderContext.Theme.TextColor`
- Supported sizes: 4×2, 4×1, 2×1 and 1×1

#### Scenario: Clock updates every second
- **WHEN** the Clock widget is configured with `[RefreshOnTick(1, TimeUnit.Seconds)]`
- **THEN** the displayed time updates every second

#### Scenario: Clock renders at different sizes
- **WHEN** the Clock widget is placed at different grid sizes (e.g., 4×2, 4×1, 2×1, 1×1)
- **THEN** font sizes are proportional to the surface height, and text shrinks to fit within 85% of the width so it never clips on narrow sizes

### Requirement: Clock Configuration
The Clock widget MUST support the following configuration parameters (set as extra properties on its JSON widget object):

- `format` — "24h" or "12h" (default: "24h")
- `showDate` — boolean (default: true)
- `textColor` — hex color override (default: use theme color)
- `fontSize` — float multiplier (default: 1.0, scales relative to the size-derived font)

#### Scenario: Clock renders in 12-hour format
- **WHEN** a widget is configured with `format: "12h"` and the current time is 19:30
- **THEN** the displayed time is "7:30 PM"

#### Scenario: Clock hides date line
- **WHEN** a widget is configured with `showDate: false`
- **THEN** only the time is displayed (no date line below)

### Requirement: Clock Visual Appearance
The Clock widget MUST produce a visually clean, modern digital clock:

- A rounded "panel card" (theme `PanelBackgroundColor`, small inset and corner radius proportional to the smaller surface dimension) is drawn behind the text; the surface outside the card is transparent over the window background
- The time uses Segoe UI Semibold; the date uses Segoe UI Light at ~80% alpha and a smaller size (falling back to the default typeface if Segoe UI is unavailable)
- The time/date block is centered horizontally and vertically using font cap-height metrics
- Colors come from the current theme via `WidgetRenderContext.Theme`

Design note: the panel card and font choices are currently decided inside the widget. The user approved the card over the original "transparent background" requirement. This styling should move to a shared theme/style engine (see the `theme-engine` change).

#### Scenario: Clock renders with default theme
- **WHEN** the Clock widget is used with the default theme
- **THEN** a card in `PanelBackgroundColor` is drawn behind text in the theme's `TextColor`
