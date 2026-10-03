## MODIFIED Requirements

### Requirement: Clock Display
The Clock widget (`typeId` `urdeck.widgets.clock`) MUST display the current time and date on its SkiaSharp surface:

- Primary display: `HH:mm` (24-hour clock by default, configurable to 12-hour `h:mm` with `AM`/`PM` shown as a unit)
- Secondary display: Date line, formatted `ddd MMM d, yyyy`
- The time is drawn with the shared readout component and the date with the shared text line component (see the
  `components` capability), centered horizontally
- Color: the config `TextColor` override if valid, otherwise the theme's text colour
- Supported sizes: 4×2, 4×1, 2×1 and 1×1

#### Scenario: Clock ticks every second but repaints once a minute
- **WHEN** the Clock widget is configured with `[RefreshOnTick(1, TimeUnit.Seconds)]`
- **THEN** it refreshes every second so the minute rollover is timely, but `NeedsRender` returns `true` only when the displayed minute changes, so it repaints at most once a minute

#### Scenario: Clock renders at different sizes
- **WHEN** the Clock widget is placed at different grid sizes (e.g., 4×2, 4×1, 2×1, 1×1)
- **THEN** the time is as large as fits the content rectangle, the date uses the theme's title size, and neither is clipped on narrow sizes

#### Scenario: Time does not resize as digits change
- **WHEN** the displayed time changes from `11:11` to `20:00`
- **THEN** the time is drawn at the same text size

### Requirement: Clock Configuration
The Clock widget MUST support the following configuration parameters (set as extra properties on its JSON widget object):

- `format` — "24h" or "12h" (default: "24h")
- `showDate` — boolean (default: true)
- `textColor` — hex color override (default: use theme color)
- `fontSize` — float multiplier applied to the time's fitted size (default: 1.0); the time never exceeds the content rectangle, so values above 1.0 have no further effect

#### Scenario: Clock renders in 12-hour format
- **WHEN** a widget is configured with `format: "12h"` and the current time is 19:30
- **THEN** the displayed time is "7:30" with the unit "PM"

#### Scenario: Clock hides date line
- **WHEN** a widget is configured with `showDate: false`
- **THEN** only the time is displayed (no date line below)

#### Scenario: Font size multiplier below one
- **WHEN** a widget is configured with `fontSize: 0.5`
- **THEN** the time is drawn at half the size it would otherwise fit at

### Requirement: Clock Visual Appearance
The Clock widget MUST take its whole appearance from the theme and the shared components:

- The card behind the text is drawn by the host (see widget-card); the Clock draws no background of its own
- Fonts, weights, the date's size and the muted date colour come from the theme; the Clock names no font and no fixed
  size
- The time/date block is centered horizontally and vertically inside the content rectangle

#### Scenario: Clock renders with default theme
- **WHEN** the Clock widget is used with the default theme
- **THEN** the host-drawn card is behind the time in the theme's text colour and the date in the theme's muted text colour

#### Scenario: Clock follows a theme change
- **WHEN** the active theme changes to one with a different font and text colour
- **THEN** the Clock shows the new font and colour without any change to the widget
