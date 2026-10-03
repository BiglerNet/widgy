## ADDED Requirements

### Requirement: Theme Application
The host MUST apply the selected theme (see the `theme` capability) to everything it draws:

- The theme is selected on startup and again on every configuration reload
- The window background uses the theme's background colour
- Every widget surface receives the same theme, resolved for the target monitor's physical pixels
- When the theme or the monitor's size or scaling changes, the page is rebuilt so that layout, cards and widgets all
  use the new values

#### Scenario: Window background follows the theme
- **WHEN** the active theme changes from `default-dark` to `default-light`
- **THEN** the window background changes to the light theme's background colour

#### Scenario: Display scaling changes
- **WHEN** the target monitor's scaling changes while the host runs
- **THEN** the page is rebuilt and cards keep the same proportions relative to the grid cell

## MODIFIED Requirements

### Requirement: SkiaSharp Element Integration
The host MUST use `SkiaSharp.Views.WPF.SKElement` for widget rendering:

- One `SKElement` per widget, positioned on a WPF `Canvas` at the widget's card rectangle (see grid-layout)
- There is NO page-level canvas
- `SKElement` rasterizes in software into a `WriteableBitmap` at the monitor's physical resolution
- The host draws the widget's card on the element before the widget renders (see widget-card)
- Widgets MUST NOT draw outside their assigned element bounds
- A widget whose `Render` throws is drawn as a themed error card and the failure is logged; other widgets are unaffected

#### Scenario: Widget renders to its own element
- **WHEN** a widget's `Render()` is called
- **THEN** the `Canvas` in `WidgetRenderContext` is that widget's own surface, already showing its card, and
  `PixelSize` is the surface's pixel size

#### Scenario: Widget render throws
- **WHEN** a widget throws inside `Render()`
- **THEN** an error card with the widget name and message is drawn in its place and the exception is logged

### Requirement: Application Entry Point
The host MUST provide the application entry point with:

- Startup sequence: monitor enumeration, config load, theme selection, plugin scan, target selection, window/layout,
  per-widget timers
- Unhandled UI exceptions logged via `UrDeckLog`
- Diagnostics written to `urdeck.log` next to the executable (no console output)
- `--snapshot out.png [--size WxH] [--theme name]`: renders the active page off-screen via `PageRenderer` with the same
  layout, card and widget code, writes a PNG (default size = target monitor resolution, default theme = the configured
  theme) and exits with 0 on success, 1 on failure

#### Scenario: Application starts clean
- **WHEN** the user launches the urdeck host
- **THEN** the window appears on the target monitor with configured widgets
- **THEN** widgets render at their configured refresh rates

#### Scenario: Snapshot
- **WHEN** the host is run with `--snapshot out.png --size 1100x3840`
- **THEN** a 1100×3840 PNG of the active page is written and no window is shown

#### Scenario: Snapshot with a named theme
- **WHEN** the host is run with `--snapshot out.png --theme default-light`
- **THEN** the PNG shows the page under the light theme without the configuration being changed
