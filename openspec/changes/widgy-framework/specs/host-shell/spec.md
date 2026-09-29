# Host Shell Specification

## ADDED Requirements

### Requirement: WPF Host Window
The host application MUST provide a full-screen WPF window that:

- Occupies the target monitor's full client area at startup
- Uses `WindowStyle.None` and `WindowStartupLocation.CenterScreen`
- Sits in normal Z-order (behind other windows, only above the taskbar/start bar on the monitor)
- Updates its size when the monitor resolution changes
- Does NOT use window transparency

#### Scenario: Window covers full monitor
- **WHEN** the host starts on the target monitor
- **THEN** the window size matches the monitor's work area dimensions
- **THEN** the window appears behind any windows the user drags onto the monitor

### Requirement: SkiaSharp Canvas Integration
The host MUST use `SkiaSharp.Views.WPF.SkiaCanvas` for widget rendering:

- One `SkiaCanvas` per page (rendered full-screen)
- One `SkiaCanvas` per widget (each widget gets its own canvas sized to its grid cell)
- Widgets MUST NOT draw outside their assigned canvas bounds
- Canvas rendering is GPU-accelerated via SkiaSharp's native backend

#### Scenario: Widget renders to its own canvas
- **WHEN** a widget's `RenderAsync()` is called
- **THEN** the `Canvas` parameter in `WidgetRenderContext` is the widget's assigned SkiaCanvas

### Requirement: Plugin Directory Setup
The host MUST discover plugins in the `plugins/` subdirectory of its executable:

- Scans for `.dll` files on startup
- Uses `Assembly.LoadFrom()` to load each assembly
- Uses `FileSystemWatcher` to detect file changes for hot-reload
- Debounces file change events by 500ms to prevent rapid reload loops
- Logs warnings for assembly load failures and continues loading remaining plugins

#### Scenario: Plugin loads successfully
- **WHEN** a valid plugin DLL with an `IWidget<TConfig>` implementation is in `plugins/`
- **THEN** the widget is registered in the registry

#### Scenario: Plugin fails to load
- **WHEN** a DLL in `plugins/` is invalid or missing dependencies
- **THEN** the host logs a warning to the console and continues

### Requirement: Configuration Management
The host MUST manage configuration via `widgy-config.json`:

- Located in the application directory at startup
- Stored in `widgy-config.json` if it exists at startup, or created if it does not exist
- Saves configuration on close and on any layout change
- Hot-reloads configuration on file change (debounce 1s)
- Validates configuration schema on load

#### Scenario: Config file does not exist
- **WHEN** the host starts with no `widgy-config.json`
- **THEN** the host creates an empty config with one default page and runs in "default mode"

### Requirement: Application Entry Point
The host MUST provide the application entry point with:

- Initialization of `Application`, `SkiaSharp`, and `WPF` resources
- Monitor detection to identify the target monitor
- Plugin loading via the `WidgetPluginLoader` service
- Grid layout computation via the `GridLayoutManager`
- Configuration loading
- Startup sequence: monitor → config → plugins → layout → render loop

#### Scenario: Application starts clean
- **WHEN** the user launches the widgy host
- **THEN** the window appears on the target monitor with configured widgets
- **THEN** widgets render at their configured refresh rates
