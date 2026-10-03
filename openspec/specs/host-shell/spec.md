# Host Shell Specification

## Purpose

Defines the WPF host application: monitor selection and window placement, one rendering surface per widget, refresh scheduling, plugin discovery and hot-reload, and the application icon.

## Requirements

### Requirement: WPF Host Window
The host application MUST provide a borderless WPF window that:

- Covers the target monitor's full physical bounds (not the work area) exactly, positioned with `SetWindowPos`
- Uses `WindowStyle.None`, `ResizeMode.NoResize` and manual startup location
- Is process-wide PerMonitorV2 DPI aware (application manifest); monitors are enumerated in physical pixels
- Re-applies the exact bounds after a `DpiChanged` event (WPF rescales windows that cross monitors of different DPI)
- Sits in normal Z-order (does not use topmost) and does NOT use window transparency
- Closes on Escape

#### Scenario: Window covers full monitor
- **WHEN** the host starts on the target monitor
- **THEN** the window's physical bounds equal the monitor's full bounds
- **THEN** a mismatch between the window and target bounds is logged as a warning

#### Scenario: Window survives display changes
- **WHEN** display settings change, or the machine resumes from sleep
- **THEN** the host re-selects the target monitor and re-covers it, re-checking after 1.5s and again after 5s because monitors wake in arbitrary order

### Requirement: Monitor Selection
The host MUST choose the target monitor from configuration:

- `monitorName` may be `primary`, `tallest` (tallest portrait monitor), `widest`, `largest` (by pixel area), or a device name suffix such as `DISPLAY1`
- If `monitorName` is unset or matches nothing, the 1-based `monitor` index is used
- If that also fails, the primary monitor is used
- A config change re-runs selection and moves the window without restarting

#### Scenario: Tallest monitor selected
- **WHEN** `monitorName` is `"tallest"` and a 1100×3840 portrait panel is attached
- **THEN** the window covers that panel

### Requirement: SkiaSharp Element Integration
The host MUST use `SkiaSharp.Views.WPF.SKElement` for widget rendering:

- One `SKElement` per widget, positioned on a WPF `Canvas` at the widget's grid rectangle
- There is NO page-level canvas
- `SKElement` rasterizes in software into a `WriteableBitmap` at the monitor's physical resolution
- Widgets MUST NOT draw outside their assigned element bounds
- A widget whose `Render` throws is drawn as a visible error tile and the failure is logged; other widgets are unaffected

#### Scenario: Widget renders to its own element
- **WHEN** a widget's `Render()` is called
- **THEN** the `Canvas` in `WidgetRenderContext` is that widget's own surface and `PixelSize` is the surface's pixel size

#### Scenario: Widget render throws
- **WHEN** a widget throws inside `Render()`
- **THEN** an error tile with the widget name and message is drawn in its cell and the exception is logged

### Requirement: Widget Refresh Scheduling
The host MUST drive each widget from its own refresh policy, with no global render loop:

- `RefreshOnTick` — a per-widget `DispatcherTimer` at the declared interval; each tick awaits `UpdateAsync` and then repaints
- `RefreshAdaptive` — a timer at `MinMs` (load-based scaling up to `MaxMs` is specified by a later change)
- `RefreshOnEvent` — the widget renders once on load (event-driven refresh is specified by a later change)
- Overlapping updates for one widget are skipped, not queued
- After each refresh the host asks `IWidget.NeedsRender(now)` and repaints only when it returns `true`; a widget is always painted when first placed, after a config change and after a page rebuild

#### Scenario: Idle dashboard
- **WHEN** only a 1-second Clock is placed
- **THEN** the only recurring work is that widget's 1s timer (measured idle CPU ~0.03%)

#### Scenario: Unchanged widget skips repaint
- **WHEN** a 1-second Clock ticks again within the same displayed minute
- **THEN** `UpdateAsync` runs but `NeedsRender` returns `false` and the widget is not repainted

### Requirement: Application Icon
The host executable MUST embed an application icon so the executable, taskbar and window show it. The current artwork is a placeholder.

#### Scenario: Icon is present
- **WHEN** the built `UrDeck.Host.exe` is inspected
- **THEN** it carries an embedded icon

### Requirement: Plugin Directory Setup
The host MUST discover plugins in the `plugins/` subdirectory of its executable:

- Creates `plugins/` if missing and scans it for `.dll` files on startup
- Loads each assembly into its own collectible `AssemblyLoadContext` from a shadow copy under the system temp folder (`urdeck-shadow/<pid>/`), so the original file is never locked; `UrDeck.Sdk`, SkiaSharp and the framework resolve from the default context
- Uses `FileSystemWatcher` to detect changes and hot-reloads: unloads old contexts, re-registers widgets, and rebuilds the page (`PluginsChanged`)
- Debounces file change events by 500ms
- Logs warnings for assembly load failures and continues loading remaining plugins
- Rejects widget types that fail descriptor validation (see widget-sdk) with a logged reason
- Removes shadow directories left by dead processes at startup

#### Scenario: Plugin loads successfully
- **WHEN** a valid plugin DLL with a widget implementation is in `plugins/`
- **THEN** the widget is registered in the registry

#### Scenario: Plugin fails to load
- **WHEN** a DLL in `plugins/` is invalid or missing dependencies
- **THEN** the host logs a warning to `urdeck.log` and continues

#### Scenario: Plugin replaced while running
- **WHEN** a plugin DLL in `plugins/` is overwritten or deleted while the host runs
- **THEN** the original file is not locked, the plugin is reloaded (or removed) after the debounce, and the page is rebuilt

#### Scenario: Unknown widget type in config
- **WHEN** the config references a `typeId` with no registered widget
- **THEN** its cell stays empty and a warning listing the registered ids is logged

### Requirement: Configuration Management
The host MUST manage configuration via `urdeck-config.json`:

- Located next to the executable; created with defaults if it does not exist
- Saves configuration on exit
- Hot-reloads configuration on file change (1s debounce), retrying when the file is briefly locked and ignoring its own saves
- On a parse error keeps running with the last good configuration and logs a warning
- Preserves widget-specific settings (extra JSON properties on a widget object) across load/save

#### Scenario: Config file does not exist
- **WHEN** the host starts with no `urdeck-config.json`
- **THEN** the host creates a default config with one empty page and runs

#### Scenario: Config edited while running
- **WHEN** the user changes a widget setting (e.g. `format` to `"12h"`) or `monitorName` and saves
- **THEN** the host reloads, re-targets the monitor if needed, and rebuilds the page

#### Scenario: Invalid config edit
- **WHEN** the file is saved with invalid JSON
- **THEN** the host logs a warning and keeps the previous configuration

### Requirement: Application Entry Point
The host MUST provide the application entry point with:

- Startup sequence: monitor enumeration, config load, plugin scan, target selection, window/layout, per-widget timers
- Unhandled UI exceptions logged via `UrDeckLog`
- Diagnostics written to `urdeck.log` next to the executable (no console output)
- `--snapshot out.png [--size WxH]`: renders the active page off-screen via `PageRenderer` with the same layout and widget code, writes a PNG (default size = target monitor resolution) and exits with 0 on success, 1 on failure

#### Scenario: Application starts clean
- **WHEN** the user launches the urdeck host
- **THEN** the window appears on the target monitor with configured widgets
- **THEN** widgets render at their configured refresh rates

#### Scenario: Snapshot
- **WHEN** the host is run with `--snapshot out.png --size 1100x3840`
- **THEN** a 1100×3840 PNG of the active page is written and no window is shown
