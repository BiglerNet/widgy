# UrDeck Framework — Implementation Tasks

Status legend: `[x]` done and verified against the code; `[ ]` open. Task wording was updated to match what was actually implemented (.NET 10, `SKElement`, collectible `AssemblyLoadContext`, ...).

## 1. Project Structure Setup

- [x] 1.1 Create `.gitignore` with .NET/WPF/SkiaSharp ignores (bin, obj, packages, NuGet, .vs, .idea, *.user, node_modules, urdeck.log, /plugins/, agent scratch files). `urdeck-config.json` is deliberately NOT ignored (see 9.8)
- [x] 1.2 Create `UrDeck.Host/` and `.csproj` (WPF app, `net10.0-windows10.0.19041.0`, WinExe, app manifest). The Windows SDK suffix is required so SkiaSharp.Views.WPF resolves its .NET build
- [x] 1.3 Create `UrDeck.Core/` and `.csproj` (class library, `net10.0`)
- [x] 1.4 Create `UrDeck.Analyzer/` and `.csproj` (Roslyn analyzer, `netstandard2.0`)
- [x] 1.5 Create `UrDeck.Widgets.Clock/` and `.csproj` (class library, `net10.0`, plugin DLL)
- [x] 1.6 Create `urdeck.sln` with the 4 product projects plus `UrDeck.Core.Tests` and `UrDeck.Analyzer.Tests`
- [x] 1.7 Provide a `plugins/` directory where runtime plugin DLLs land (created next to the executable by the host build's `CopyBuiltInPlugins` target and by the loader at startup)
- [x] 1.8 Add project reference from UrDeck.Host → UrDeck.Core
- [x] 1.9 Reference UrDeck.Analyzer as an analyzer (`OutputItemType=Analyzer`, `ReferenceOutputAssembly=false`) from plugin projects
- [x] 1.10 Add project reference from UrDeck.Widgets.Clock → UrDeck.Core (`Private="false"`, host supplies it at runtime)
- [x] 1.11 Add SkiaSharp 3.119.4 to UrDeck.Core and SkiaSharp + SkiaSharp.Views.WPF 3.119.4 to UrDeck.Host
- [x] 1.12 Add Microsoft.CodeAnalysis.Analyzers and Microsoft.CodeAnalysis.CSharp packages to UrDeck.Analyzer
- [x] 1.13 Build all projects to verify compilation succeeds without errors (`dotnet build urdeck.sln -c Release`: 0 warnings, 0 errors)

## 2. Core Widget SDK (UrDeck.Core)

### 2.1: Widget Metadata Classes

- [x] 2.1.1 `Attributes/WidgetAttribute.cs` — `[Widget(name, description)]` with `Category`, `ConfigType` and `Id` (stable typeId, falls back to Name)
- [x] 2.1.2 `Attributes/WidgetSizeAttribute.cs` — `[WidgetSize(width, height)]` (multiple allowed)
- [x] 2.1.3 `Attributes/RefreshOnTickAttribute.cs` — `[RefreshOnTick(interval, TimeUnit)]`
- [x] 2.1.4 `Attributes/RefreshAdaptiveAttribute.cs` — `[RefreshAdaptive(minMs, maxMs)]`
- [x] 2.1.5 `Attributes/RefreshOnEventAttribute.cs` — `[RefreshOnEvent(eventName)]`
- [x] 2.1.6 `Attributes/CategoryAttribute.cs` — `[Category(name)]`
- [x] 2.1.7 `Attributes/RefreshStrategy.cs` — enum: None, OnTick, OnEvent, Adaptive
- [x] 2.1.8 `Enums/TimeUnit.cs` — enum: Seconds, Minutes, Hours, Milliseconds
- [x] 2.1.9 `Config/WidgetConfig.cs` — base class with typeId, Col, Row, Width, Height, Parameters, IsVisible, `[JsonExtensionData]` for widget-specific settings, `ToConcrete(Type)`, `SaveConfigJson()`/`LoadConfigJson(string)`
- [x] 2.1.10 `Widget.cs` — abstract `Widget<TConfig>` base class: metadata defaults from attributes, `Configure`, virtual `UpdateAsync`, abstract `Render`

### 2.2: Widget Interface and Runtime Context

- [x] 2.2.1 `Interfaces/IWidget.cs` — non-generic `IWidget` (Name, Description, Category, SupportedSizes, ConfigType, Config, `Configure`, `UpdateAsync`, `Render`) and `IWidget<TConfig>` (typed Config, DefaultConfig)
- [x] 2.2.2 `Rendering/WidgetRenderContext.cs` — Canvas (SKCanvas), Time, PixelSize, Theme, Config, CancellationToken
- [x] 2.2.3 `Rendering/ThemeColors.cs` — record struct with TextColor, BackgroundColor, AccentColor, PanelBackgroundColor, PanelHeaderColor and a `DefaultDark` palette
- [x] 2.2.4 Verify all core types compile and expose public APIs via `dotnet build UrDeck.Core`
- [x] 2.2.5 `Config/ConfigStore.cs` for urdeck-config.json with hot-reload via FileSystemWatcher (1000ms debounce, locked-file retry, last-good-config on parse error, ignores own saves)
- [x] 2.2.6 `Rendering/WidgetPainter.cs` — `RenderSafely` draws a visible error tile when a widget throws; `Rendering/PageRenderer.cs` renders a page off-screen (used by `--snapshot`); `Diagnostics/UrDeckLog.cs` file logger

## 3. Code Analyzer (UrDeck.Analyzer)

- [x] 3.1 `WidgetAnalyzer.cs` — DiagnosticAnalyzer for classes deriving from `Widget<TConfig>` (direct or indirect, non-abstract) with IDs URDECK001 (missing [Widget]), URDECK002 (missing [WidgetSize]), URDECK003 (no refresh strategy), URDECK004 (multiple refresh strategies), URDECK005 (invalid size)
- [x] 3.2 Analysis logic: `[Widget]` required (error), at least one `[WidgetSize]` (error), exactly one refresh attribute (error), width 1–4 and height ≥ 1 (error). Attributes matched by symbol
- [x] 3.3 `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`
- [x] 3.4 Build UrDeck.Analyzer (netstandard2.0 analyzer assembly)
- [x] 3.5 Verify the analyzer emits diagnostics — covered by `UrDeck.Analyzer.Tests` (in-memory compilation, URDECK001–005, 12 tests) rather than a broken widget in the Clock project

## 4. Plugin Loader (UrDeck.Core)

- [x] 4.1 `Plugin/WidgetPluginLoader.cs` — scans a plugin directory for DLLs and loads each into its own collectible `AssemblyLoadContext` (`Plugin/PluginLoadContext.cs`) from a shadow copy under `%TEMP%/urdeck-shadow/<pid>/`; contract assemblies resolve from the default context
- [x] 4.2 Reflection scanning: concrete, non-interface types assignable to `IWidget` are turned into a `WidgetDescriptor` (`WidgetDescriptor.TryCreate`); types missing [Widget]/[WidgetSize], with != 1 refresh strategy, no parameterless constructor or unknown config type are rejected with a logged reason
- [x] 4.3 `Plugin/IPluginService.cs` — ScanAndLoadPlugins, ReloadPlugins, GetRegisteredWidgetTypes, GetDescriptor, CreateWidget, `PluginsChanged` event
- [x] 4.4 `Plugin/WidgetRegistry.cs` — thread-safe registry of type id → `WidgetDescriptor`; `CreateWidget` builds a new configured instance per placed widget (instances are not cached)
- [x] 4.5 Hot-reload: FileSystemWatcher on the plugin directory, 500ms debounce, unload old contexts, re-register, raise `PluginsChanged` (host rebuilds the page); stale shadow directories cleaned at startup
- [x] 4.6 Verify plugin loading and metadata via `UrDeck.Core.Tests/PluginLoaderTests.cs` (own context with shared contract types, original file unlocked, reload picks up replaced plugin, reload after delete, unloaded context collectible off-screen, garbage DLL skipped)

## 5. Grid Layout Engine (UrDeck.Core)

- [x] 5.1 `Layout/GridCell.cs` — Col, Row, Width, Height plus `Overlaps`/`Contains` helpers
- [x] 5.2 `Layout/GridLayoutManager.cs` — ColumnWidth, RowHeight, `ConvertToPixels`, `ConvertSizeToPixels`, `ValidatePosition`, `RenderWidgetLayout(widgets, screenDims)` returning `List<WidgetLayoutItem>`
- [x] 5.3 `Layout/WidgetLayoutItem.cs` — WidgetTypeId, Position, Size, GridSize
- [x] 5.4 Layout validation: clamp width to 1–4 first, then col to `0..4-width`, row ≥ 0, height ≥ 1, and log a warning when clamping (logged via `UrDeckLog`)
- [x] 5.5 Monitor changes recalculate the grid: the layout is a pure function of screen size and the host rebuilds it whenever the window size changes (monitor retarget, DPI change, config change). The originally planned `MonitorChanged` event / `RecalculateColumns()` were dropped as unnecessary (see 10.6)
- [x] 5.6 Verify grid conversion for 1100 (275px cells), 3840×2160 and 7680×2160 in `UrDeck.Core.Tests/GridLayoutTests.cs`, using the corrected spec values

## 6. Host Shell (UrDeck.Host)

- [x] 6.1 `UrDeck.Host/MainWindow.cs` — code-only WPF window (no XAML): `WindowStyle.None`, `ResizeMode.NoResize`, solid theme background, WPF `Canvas` as content
- [x] 6.2 MainWindow lifecycle: SourceInitialized placement, DpiChanged re-cover, SizeChanged → coalesced layout rebuild, config/plugin change handlers, display-settings and resume handlers, cleanup on close
- [x] 6.3 `UrDeck.Host/App.xaml` and `App.xaml.cs` — OnStartup: monitors → config → plugins → target selection → `--snapshot` or main window; saves config and disposes on exit
- [x] 6.4 `UrDeck.Host/Resources/DefaultTheme.xaml` — default dark theme resources, merged in `App.xaml`
- [x] 6.5 Monitor detection: Win32 `EnumDisplayMonitors`/`GetMonitorInfo` in physical pixels (`MonitorPlacement.cs`), selecting by `monitorName`/`monitor` (not WinForms `Screen`)
- [x] 6.6 Plugin directory setup: loader creates `plugins/` next to the executable if missing
- [x] 6.6b Configuration management: `UrDeck.Core/Config/ConfigStore.cs` (System.Text.Json, hot-reload, 1000ms debounce)
- [x] 6.7 SkiaSharp integration: one `SKElement` per widget (`WidgetView.cs`) placed on a WPF `Canvas` (software-rasterized `WriteableBitmap` at the monitor's physical resolution; no page-level canvas)
- [x] 6.8 Widget refresh: per-widget `DispatcherTimer` from the refresh attribute (no global loop); each tick awaits `UpdateAsync` then repaints; `RenderSafely` per paint
- [x] 6.9 Window sizing: `SetWindowPos` to exactly cover the target monitor's full physical bounds, re-applied after `DpiChanged`; re-targets on display-settings change, resume (re-check at 1.5s and 5s) and config change
- [x] 6.10 Per-monitor DPI awareness via `app.manifest` (PerMonitorV2)
- [x] 6.11 `--snapshot out.png [--size WxH]` off-screen render through `PageRenderer`

## 7. Built-in Clock Widget (UrDeck.Widgets.Clock)

- [x] 7.1 `ClockConfig.cs` — extends WidgetConfig with Format ("24h"), ShowDate (true), TextColor (null), FontSize (1.0)
- [x] 7.2 `ClockWidget.cs` — derives from `Widget<ClockConfig>`, decorated with `[Widget("Clock", "Display current time", Id = "urdeck.widgets.clock")]`, `[WidgetSize]` 4×2, 4×1, 2×1, 1×1, `[RefreshOnTick(1, TimeUnit.Seconds)]`, `[Category("System")]`
- [x] 7.2.3 Metadata (Name, Description, Category, SupportedSizes, ConfigType) comes from the attributes via `Widget<TConfig>`
- [x] 7.2.4 Implement `Render`: font sizes proportional to surface height (time ≈ 0.30, date ≈ 0.15), text centered horizontally and vertically using cap-height metrics, color from the config override or `Theme.TextColor`
- [x] 7.2.5 24-hour/12-hour format support ("HH:mm" vs "h:mm tt")
- [x] 7.2.6 Date line (`ddd MMM d, yyyy`) when `ShowDate`, drawn in Segoe UI Light at ~80% alpha
- [x] 7.2.7 Text scaling from surface height and the `FontSize` multiplier; shrink-to-fit within 85% of the width for narrow sizes
- [x] 7.2.8 Time (Segoe UI Semibold) and date drawn centered
- [x] 7.2.9 Background: a rounded "panel card" in `Theme.PanelBackgroundColor` (small inset/radius) instead of the originally specified transparent/full-canvas background; approved by the user. Moving this into a shared theme engine is proposed in the `theme-engine` change
- [x] 7.3 `UrDeck.Widgets.Clock.csproj` — class library, `net10.0`
- [x] 7.4 Build UrDeck.Widgets.Clock
- [x] 7.5 The host build copies the Clock DLL to `<host output>/plugins/` (`CopyBuiltInPlugins` target)

## 8. End-to-End Integration

- [x] 8.1 `UrDeck.Host/urdeck-config.json` with a default page containing Clock at col 0, row 0, 4×2, format "24h", showDate true, and `monitorName: "tallest"`
- [x] 8.2 Build UrDeck.Host and confirm UrDeck.Widgets.Clock.dll lands in `plugins/`
- [x] 8.3 Run UrDeck.Host and verify the window opens on the target monitor showing a centered digital clock (verified on the real 1100×3840 panel)
- [x] 8.4 Verify the clock updates every second
- [x] 8.4.5 Verify the grid layout on the 1100×3840 vertical panel (widget fills the full width, 2 rows tall)
- [x] 8.5 Hot-reload urdeck-config.json (verified: switching `format` and switching the target monitor)
- [x] 8.6 Hot-reload plugins (verified: removing and restoring the plugin DLL while running)
- [x] 8.7 Plugin/unknown-type failure handling (verified: unknown widget type leaves an empty cell with a warning; removing the Clock plugin leaves an empty page)
- [x] 8.8 Recalculation on monitor change (verified by re-targeting to a 1080×1920 monitor; the grid is recomputed). Manual window resizing is not supported: the window is borderless and fixed to the monitor

## 9. Final Polish

- [x] 9.1 Add application icon (.ico): placeholder `src/UrDeck.Host/Resources/urdeck.ico`, set as `ApplicationIcon` (verified: extracted from the built exe)
- [x] 9.3 Remove debug output: Console/debug writes replaced by `UrDeckLog` file logging (no `Console.Write` calls remain)
- [x] 9.4 Test with actual display hardware — done on the HYTE Y70 Touch 1100×3840 panel
- [x] 9.5 Verify `urdeck.sln` builds in Release mode (`dotnet build urdeck.sln -c Release`: succeeded, 0 warnings, 0 errors)
- [x] 9.6 `UrDeck.Host/Resources/DefaultTheme.xaml` exists for application-wide styles (font family, etc.)
- [x] 9.7 Add `README.md` to the project root explaining how to build and run
- [x] 9.8 N/A by design: the source default config `UrDeck.Host/urdeck-config.json` is intentionally tracked; the runtime copy the build places under `bin/` is already ignored via `[Bb]in/`
- [x] 9.9 Reference UrDeck.Analyzer from `UrDeck.Widgets.Clock.csproj` (compile-time validation)
- [x] 9.10 Open questions from design.md are resolved or recorded in its Open Questions section (memory, event bus and adaptive refresh were resolved or moved, see section 11)

## 10. Follow-ups

- [x] 10.4 Unloaded plugin contexts are collected in the live WPF host (fixed WPF `SafeSecurityHelper` cache pinning and System.Text.Json caching; verified ~1.9s after unload)
- [x] 10.6 Small cleanups: layout clamp warning goes through `UrDeckLog`; unused `GridLayoutManager.MonitorChanged` event removed
- [x] 10.9 Skip redundant redraws: `IWidget.NeedsRender(DateTime now)` (default `true`, virtual on `Widget<TConfig>`); the host repaints only when it returns `true`; the Clock repaints once a minute
- [x] 10.10 Close-out: placeholder icon builds into the exe (9.1), specs synced into `openspec/specs`, roadmap "Current state" updated, change archived

## 11. Moved out of this change

Open when the framework change was closed; tracked in `docs/ROADMAP.md`:

- Icon from the `icon` config field and a tray icon (was 9.2): roadmap item 12 (packaging and distribution).
- `[RefreshOnEvent]` event bus (was 10.1) and `[RefreshAdaptive]` load-based scaling (was 10.2): the `data-providers` change (roadmap item 3).
- Memory goal (was 10.3): decided to keep the WPF host and treat about 66 MB private (60-70 MB) as the baseline. Budgets and measurement are roadmap item 4; a Win32 + Skia host is revisited only if that budget proves unacceptable.
- Consuming the `dock`, `theme` and `background` config fields (was 10.5): theme in the theme engine (roadmap item 5), backgrounds and dock in roadmap items 9 and 10.
- Shared widget styling / theme engine (was 10.7): roadmap item 5.
- Monitor picker and robust display handling (was 10.8): roadmap item 6.
