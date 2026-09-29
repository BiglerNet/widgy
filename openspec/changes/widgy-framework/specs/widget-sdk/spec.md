# Widget SDK Specification

## ADDED Requirements

### Requirement: Widget Metadata Attributes
Every widget MUST be decorated with metadata attributes that describe its identity, sizing, and refresh strategy. The following attributes are required:

- `[Widget(name, description)]` — Identifies the widget. REQUIRED on all widget classes. The optional `Id` property is the stable type id used as `typeId` in `widgy-config.json` (falls back to `Name`); authors SHOULD set it (e.g. `Id = "widgy.widgets.clock"`).
- `[WidgetSize]` — Specifies one or more valid (width, height) grid unit sizes the widget supports. At least one `[WidgetSize]` is REQUIRED.
- Exactly one of `[RefreshOnTick]`, `[RefreshAdaptive]`, or `[RefreshOnEvent]` — Declares the widget's refresh strategy. REQUIRED.
- `[Category]` — Groups the widget in the editor UI (optional but recommended for discoverability).

A widget implementation that is missing any required attribute or has conflicting attributes MUST NOT be usable at runtime.

#### Scenario: Widget with all required attributes
- **WHEN** a class is decorated with `[Widget]`, at least one `[WidgetSize]`, and exactly one refresh attribute
- **THEN** the plugin loader registers the widget successfully under its `Id`

#### Scenario: Widget without explicit Id
- **WHEN** `[Widget("Clock", "...")]` omits `Id`
- **THEN** the widget is registered under its `Name`

#### Scenario: Widget missing required [Widget] attribute
- **WHEN** a widget class lacks the `[Widget]` attribute
- **THEN** the plugin loader skips the class with a logged reason and the Roslyn analyzer emits a compile-time error (WIDGY001)

#### Scenario: Widget with incompatible refresh attributes
- **WHEN** a widget declares both `[RefreshOnTick]` and `[RefreshOnEvent]`
- **THEN** the Roslyn analyzer emits a compile-time error (WIDGY004) and the loader rejects the type at runtime (a count of refresh strategies other than one)

#### Scenario: Widget with no refresh strategy
- **WHEN** a widget declares no refresh attribute
- **THEN** the analyzer emits WIDGY003 and the loader rejects the type

#### Scenario: Widget with no WidgetSize
- **WHEN** a widget lacks `[WidgetSize]` entirely
- **THEN** the Roslyn analyzer emits a compile-time error (WIDGY002) and the loader rejects the type

#### Scenario: Widget with invalid size
- **WHEN** a `[WidgetSize]` has a width outside 1–4 or a height below 1
- **THEN** the analyzer emits WIDGY005

### Requirement: Widget Interface Contract
Every widget MUST implement the non-generic `IWidget` (usually via `Widget<TConfig>`, which also implements `IWidget<TConfig>`), where `TConfig` is a class derived from `WidgetConfig` with a parameterless constructor. `IWidget` MUST expose:

- `string Name`, `string Description`, `string Category` — metadata (defaulted from attributes by `Widget<TConfig>`)
- `Size[] SupportedSizes` — valid grid unit dimensions (defaulted from `[WidgetSize]` attributes)
- `Type ConfigType` — the configuration type
- `WidgetConfig Config` — the current configuration
- `void Configure(WidgetConfig config)` — applies a configuration, which MUST be an instance of `ConfigType`
- `Task UpdateAsync(CancellationToken)` — refreshes data (network, sensors, ...) off the render path; MUST NOT touch the canvas; the default does nothing
- `void Render(WidgetRenderContext context)` — draws the widget synchronously on the UI thread; the canvas is only valid for the duration of the call

`IWidget<TConfig>` additionally exposes a typed `Config` and `TConfig DefaultConfig`.

There is no `RenderAsync`.

#### Scenario: Widget derives from Widget<TConfig>
- **WHEN** a class derives from `Widget<TConfig>` and overrides only `Render`
- **THEN** its Name, Description, Category and SupportedSizes come from its attributes and the widget can be instantiated and configured by the host

#### Scenario: Wrong config type
- **WHEN** `Configure` is called with a config that is not an instance of the widget's `TConfig`
- **THEN** an `ArgumentException` is thrown

#### Scenario: Class is not a concrete widget
- **WHEN** a type in a plugin is abstract, an interface, lacks a public parameterless constructor, or does not implement `IWidget`
- **THEN** the plugin loader ignores it (or logs a reason for widget-like types) and continues

### Requirement: WidgetRenderContext
Each widget MUST receive a `WidgetRenderContext` during `Render` containing:

- `SKCanvas Canvas` — SkiaSharp canvas for 2D drawing, sized to the widget's element
- `DateTime Time` — Current timestamp at render time
- `Size PixelSize` — Widget's pixel dimensions (width, height) of the render surface
- `ThemeColors Theme` — Current theme color palette
- `WidgetConfig Config` — The widget's configuration (concrete type)
- `CancellationToken CancellationToken` — For cooperative cancellation

#### Scenario: Context is populated at render time
- **WHEN** the runtime calls `Render()`
- **THEN** `WidgetRenderContext` is fully populated with current time, surface pixel size, theme colors, and config

### Requirement: Widget Configuration Model
Widget configuration data MUST be serializable to/from JSON. The `WidgetConfig` base class MUST expose:

- `string WidgetTypeId` (JSON `typeId`) — The widget type identifier (the `[Widget]` `Id`) used to locate the correct plugin
- `int Col` — Grid column position (0-3, relative to 4-column grid)
- `int Row` — Grid row position (0-N)
- `int Width` — Width in grid columns (1-4)
- `int Height` — Height in grid rows (1-N)
- `Dictionary<string, string> Parameters` — Free-form key-value parameters
- `bool IsVisible` — Visibility flag
- Widget-specific settings as additional properties on the widget's JSON object, preserved through `[JsonExtensionData]` and converted to the concrete config type (via `ToConcrete`) when the widget is created; properties absent from the JSON keep the concrete type's defaults

#### Scenario: Config serializes to JSON
- **WHEN** `WidgetConfig` is serialized with `System.Text.Json`
- **THEN** the output contains all public properties as camelCase JSON keys

#### Scenario: Config deserializes with default values
- **WHEN** `WidgetConfig` is deserialized with `System.Text.Json`
- **THEN** properties not present in JSON retain their default values

#### Scenario: Widget-specific settings round-trip
- **WHEN** a widget object contains `"format": "12h"` and is loaded, converted to `ClockConfig`, and the config is saved
- **THEN** the concrete config has `Format == "12h"` and the saved file still contains `format`

### Requirement: Widget Registry
The runtime MUST maintain a `WidgetRegistry` that:

- Is populated at startup by scanning all loaded plugin assemblies, and rebuilt on plugin hot-reload
- Maps widget type ids (case-insensitive) to `WidgetDescriptor`s (id, metadata, widget type, config type, sizes, refresh strategy and interval, event name)
- Exposes `GetDescriptor(string typeId)` and `GetWidget(string typeId)` (returns the registered `Type`)
- Creates a new configured widget instance per placed widget (`CreateWidget(WidgetConfig)`), returning null for an unknown type

#### Scenario: Registry populates at startup
- **WHEN** the host starts and loads plugins from `plugins/`
- **THEN** all valid widgets are in the registry

#### Scenario: Registry returns widget type
- **WHEN** `GetWidget("widgy.widgets.clock")` is called
- **THEN** the correct `Type` object for ClockWidget is returned

### Requirement: Plugin Isolation and Hot-Reload
The plugin loader MUST load each plugin DLL into its own collectible `AssemblyLoadContext` from a shadow copy:

- The original DLL is never locked and can be overwritten or deleted while loaded
- `Widgy.Core`, SkiaSharp and framework assemblies resolve from the default context so widget contract types are shared with the host
- On reload, previous contexts are unloaded and the registry is rebuilt; a corrupt DLL is skipped without affecting the others
- Unloaded contexts SHOULD become collectible; the loader logs a warning if one is still alive after 4 seconds

#### Scenario: Plugin uses the host's contract types
- **WHEN** a plugin is loaded in its own context
- **THEN** its widget types are assignable to the host's `IWidget`

#### Scenario: Plugin rebuilt while running
- **WHEN** the plugin DLL is replaced and a reload occurs
- **THEN** the new version is registered and the old one is unloaded

### Requirement: Refresh Policy System
Each widget declares its refresh strategy. The runtime MUST:

- Support `[RefreshOnTick(interval, unit)]` — schedule a timer that refreshes and renders at fixed intervals (units: milliseconds, seconds, minutes, hours)
- Support `[RefreshAdaptive(minMs, maxMs)]` — schedule a timer with a dynamic interval based on system load. **Current status:** runs at `minMs`; load-based scaling is not yet implemented
- Support `[RefreshOnEvent(eventName)]` — register a callback that triggers render when a named event fires. **Current status:** not implemented; the widget renders once on load and no event channel exists yet
- Never schedule timers for widgets without a timer-based strategy
- Never allow a widget to have zero or multiple refresh strategies (compile-time and runtime error)

#### Scenario: OnTick refresh works
- **WHEN** a widget has `[RefreshOnTick(1, TimeUnit.Seconds)]`
- **THEN** the runtime schedules a timer that refreshes and renders every 1 second

#### Scenario: OnEvent refresh works (not yet implemented)
- **WHEN** a widget has `[RefreshOnEvent("perfUpdate")]`
- **THEN** the runtime listens on the "perfUpdate" event channel and triggers render only on new data
- **NOTE** Pending the event bus; today such a widget renders once on load.

#### Scenario: No refresh strategy is an error
- **WHEN** a widget has no refresh attribute
- **THEN** the Roslyn analyzer emits a compile-time error and the loader rejects the type

## REMOVED Requirements

### Requirement: WinForms Control-based Interface
- **Reason**: Replaced by SkiaSharp Canvas-based rendering
- **Migration**: N/A — this is a fresh codebase

### Requirement: GDI+ Drawing
- **Reason**: Replaced by SkiaSharp for performance and visual quality
- **Migration**: N/A — this is a fresh codebase
