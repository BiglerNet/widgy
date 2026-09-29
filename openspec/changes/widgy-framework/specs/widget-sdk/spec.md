# Widget SDK Specification

## ADDED Requirements

### Requirement: Widget Metadata Attributes
Every widget MUST be decorated with metadata attributes that describe its identity, sizing, and refresh strategy. The following attributes are required:

- `[Widget]` — Identifies the widget with a unique name and description. This is REQUIRED on all widget classes.
- `[WidgetSize]` — Specifies one or more valid (width, height) grid unit sizes the widget supports. At least one `[WidgetSize]` is REQUIRED.
- `[RefreshOnTick]`, `[RefreshAdaptive]`, or `[RefreshOnEvent]` — Declares the widget's refresh strategy. At least one is REQUIRED.
- `[Category]` — Groups the widget in the editor UI (optional but recommended for discoverability).

A widget implementation that is missing any required attribute or has conflicting attributes MUST NOT be usable at runtime.

#### Scenario: Widget with all required attributes
- **WHEN** a class is decorated with `[Widget]`, at least one `[WidgetSize]`, and a valid refresh attribute
- **THEN** the plugin loader registers the widget successfully

#### Scenario: Widget missing required [Widget] attribute
- **WHEN** a widget class lacks the `[Widget]` attribute
- **THEN** the plugin loader skips the class and the Roslyn analyzer emits a compile-time error

#### Scenario: Widget with incompatible refresh attributes
- **WHEN** a widget declares both `[RefreshOnTick]` and `[RefreshOnEvent]`
- **THEN** the Roslyn analyzer emits a compile-time error (conflicting refresh strategies)

#### Scenario: Widget with no WidgetSize
- **WHEN** a widget lacks `[WidgetSize]` entirely
- **THEN** the Roslyn analyzer emits a compile-time error

### Requirement: Widget Interface Contract
Every widget MUST implement `IWidget<TConfig>` where `TConfig` is a class derived from `WidgetConfig`. The interface MUST expose:

- `string Name` — Display name for the widget
- `string Description` — Human-readable description
- `string Category` — Editor grouping category
- `Size[] SupportedSizes` — List of valid grid unit dimensions
- `TConfig DefaultConfig` — Default configuration values
- `Type ConfigType` — The configuration type for serialization

#### Scenario: Widget implements IWidget correctly
- **WHEN** a class implements `IWidget<TConfig>` with all required properties
- **THEN** the widget is instantiated and configured by the host

#### Scenario: Widget fails to implement IWidget
- **WHEN** a class lacks required interface members
- **THEN** the plugin loader logs a warning and skips the class

### Requirement: WidgetRenderContext
Each widget receives a `WidgetRenderContext` during rendering containing:

- `Canvas Canvas` — SkiaSharp canvas for 2D drawing
- `DateTime Time` — Current timestamp at render time
- `Size PixelSize` — Widget's pixel dimensions (width, height) in grid units
- `ThemeColors Theme` — Current theme color palette
- `WidgetConfig Config` — The widget's serialized configuration
- `CancellationToken CancellationToken` — For cooperative cancellation during long renders

#### Scenario: Context is populated at render time
- **WHEN** the runtime calls `RenderAsync()`
- **THEN** `WidgetRenderContext` is fully populated with current time, computed pixel size, theme colors, and config

### Requirement: Widget Configuration Model
Widget configuration data MUST be serializable to/from JSON and stored independently per widget type. The `WidgetConfig` base class MUST expose:

- `string WidgetTypeId` — The widget type identifier (from `[Widget]`) used to locate the correct plugin
- `int Col` — Grid column position (0-3, relative to 4-column grid)
- `int Row` — Grid row position (0-N)
- `int Width` — Width in grid columns (1-4)
- `int Height` — Height in grid rows (1-N)
- `Dictionary<string, string> Parameters` — Type-specific parameters serialized as key-value pairs
- `bool IsVisible` — Visibility flag

#### Scenario: Config serializes to JSON
- **WHEN** `WidgetConfig` is serialized with `System.Text.Json`
- **THEN** the output contains all public properties as JSON keys

#### Scenario: Config deserializes with default values
- **WHEN** `WidgetConfig` is deserialized with `System.Text.Json`
- **THEN** properties not present in JSON retain their default values

### Requirement: Widget Registry
The runtime MUST maintain a `WidgetRegistry` that:

- Is populated at startup by scanning all loaded plugin assemblies
- Maps widget type IDs to their `IWidget<TConfig>` implementations
- Exposes a `GetWidget(string typeId)` method that returns the registered type
- Is accessible via `IWidgetRegistry` service from the host

#### Scenario: Registry populates at startup
- **WHEN** the host starts and loads plugins from `plugins/`
- **THEN** all valid widgets are in the registry

#### Scenario: Registry returns widget type
- **WHEN** `GetWidget("widgy.widgets.clock")` is called
- **THEN** the correct `Type` object for ClockWidget is returned

### Requirement: Refresh Policy System
Each widget declares its refresh strategy. The runtime MUST:

- Support `[RefreshOnTick(seconds)]` — schedule a timer that triggers render at fixed intervals
- Support `[RefreshAdaptive(minMs, maxMs)]` — schedule a timer with dynamic interval based on system load
- Support `[RefreshOnEvent(eventName)]` — register a callback that triggers render when a named event fires
- Never schedule timers for widgets without a refresh strategy
- Never allow a widget to have zero refresh strategy (compile-time or runtime error)

#### Scenario: OnTick refresh works
- **WHEN** a widget has `[RefreshOnTick(1, TimeUnit.Seconds)]`
- **THEN** the runtime schedules a timer that triggers render every 1 second

#### Scenario: OnEvent refresh works
- **WHEN** a widget has `[RefreshOnEvent("perfUpdate")]`
- **THEN** the runtime listens on the "perfUpdate" event channel and triggers render only on new data

#### Scenario: No refresh strategy is an error
- **WHEN** a widget has no refresh attribute at compile-time
- **THEN** the Roslyn analyzer emits a compile-time error

## REMOVED Requirements

### Requirement: WinForms Control-based Interface
- **Reason**: Replaced by SkiaSharp Canvas-based rendering
- **Migration**: N/A — this is a fresh codebase

### Requirement: GDI+ Drawing
- **Reason**: Replaced by SkiaSharp for performance and visual quality
- **Migration**: N/A — this is a fresh codebase
