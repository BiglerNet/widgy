# Clock Widget Specification

## ADDED Requirements

### Requirement: Clock Display
The Clock widget MUST display the current time and date on the SkiaSharp canvas:

- Primary display: HH:mm format (24-hour clock by default, configurable to 12-hour)
- Secondary display: Date line with day, month, year
- Font rendering: ClearType anti-aliased text via SkiaSharp's `SKPaint` with `SKTextAlign.Center`
- Color: Uses theme colors (text color from `WidgetRenderContext.Theme.TextColor`)
- Centered within the widget's SkiaCanvas bounds

#### Scenario: Clock updates every second
- **WHEN** the Clock widget is configured with `[RefreshOnTick(1, TimeUnit.Seconds)]`
- **THEN** the displayed time updates every second

#### Scenario: Clock renders at different sizes
- **WHEN** the Clock widget is placed at different grid sizes (e.g., 4×1, 4×2)
- **THEN** the font size scales proportionally to fit the available canvas

### Requirement: Clock Configuration
The Clock widget MUST support the following configuration parameters:

- `Format` — "24h" or "12h" (default: "24h")
- `ShowDate` — boolean (default: true)
- `TextColor` — hex color override (default: use theme color)
- `FontSize` — float multiplier (default: 1.0, scales relative to available canvas space)

#### Scenario: Clock renders in 12-hour format
- **WHEN** a widget is configured with `Format: "12h"` and the current time is 19:30
- **THEN** the displayed time is "7:30 PM"

#### Scenario: Clock hides date line
- **WHEN** a widget is configured with `ShowDate: false`
- **THEN** only the time is displayed (no date line below)

### Requirement: Clock Visual Appearance
The Clock widget MUST produce a visually clean, modern digital clock:

- Time and date must be centered both horizontally and vertically within the widget's canvas
- The time string should use a bold, highly legible font (Segoe UI Semibold or equivalent)
- The date line should use a lighter font weight and smaller size
- Colors must come from the current theme via `WidgetRenderContext.Theme`

#### Scenario: Clock renders with default theme
- **WHEN** the Clock widget is used with the default theme
- **THEN** the text color is derived from the theme's TextColor and the canvas background is transparent
