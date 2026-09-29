# UrDeck Framework — Phase 1: Foundation (Hello World)

## Why

HYTE Nexus is a popular PC companion display app that fills a clear market need — a customizable widget dashboard for a secondary monitor — but it is an Electron-based application that consumes excessive memory, CPU, and disk space. The user owns a custom PC case with a vertical 1100×3840 touch panel display (HYTE Y70 Touch) and wants a lightweight, beautiful replacement that:

1. Runs with minimal resource consumption (priority #1): idle widgets should consume near-zero CPU; the memory goal is to stay under 50MB (currently **not met**, see the memory risk in `design.md`).
2. Looks exceptional (priority #2): custom rendering with smooth animations, gradient support, and visually stunning gauge/chart widgets.
3. Is fully extensible: users and third-party developers should be able to create new widgets by writing a single C# plugin DLL.
4. Supports any monitor resolution dynamically: the 4-column grid layout must automatically adapt to any screen size and DPI.
5. Targets Windows only, using the .NET 10 runtime.

This proposal establishes the foundation layer: the widget SDK (metadata-driven widget contract and plugin loading system), the dynamic grid layout engine, and the WPF/SkiaSharp host shell — culminating in a running application that displays a single Clock widget on a 4-column grid on any monitor.

## What Changes

- **New Widget SDK (`UrDeck.Core`, `net10.0`, depends only on SkiaSharp)**
  - Data annotation attributes for widget metadata (`[Widget]`, `[WidgetSize]`, `[RefreshOnTick]`, `[RefreshAdaptive]`, `[RefreshOnEvent]`, `[Category]`)
  - Non-generic `IWidget` contract (metadata, `Configure`, `UpdateAsync` for data refresh off the render path, synchronous `Render` on the UI thread) plus `IWidget<TConfig>` (typed `Config`, `DefaultConfig`)
  - `Widget<TConfig>` base class whose metadata defaults come from the attributes, so a widget normally only overrides `Render`
  - Widget configuration model (`WidgetConfig` base class; widget-specific settings live as extra JSON properties)
  - Runtime context model (`WidgetRenderContext`) passed to widgets during render

- **New Roslyn Code Analyzer (`UrDeck.Analyzer`, `netstandard2.0`)**
  - Validates widget attributes at compile time (URDECK001–005): requires `[Widget]`, requires at least one `[WidgetSize]`, requires exactly one refresh strategy, validates size values
  - Emits compile-time errors (not warnings) for missing/invalid attributes
  - Zero runtime overhead: runs only during builds

- **Plugin Loader (in `UrDeck.Core`)**
  - Scans a `plugins/` directory at startup for `*.dll` files
  - Loads each plugin into its own collectible `AssemblyLoadContext` from a shadow copy, so the original DLL is never locked and can be replaced while UrDeck runs; `UrDeck.Core`, SkiaSharp and the framework resolve from the default context (shared type identity)
  - Builds a `WidgetDescriptor` per widget type (id, metadata, config type, sizes, refresh strategy + interval) and registers it in a `WidgetRegistry`; rejects types missing required attributes or with anything other than exactly one refresh strategy (runtime counterpart of the analyzer)
  - Hot-reloads: watches `plugins/` (500ms debounce), unloads old contexts, re-registers, and the host rebuilds the page
  - Creates one widget instance per placed widget

- **WPF Host Shell (`UrDeck.Host`, `net10.0-windows10.0.19041.0`)**
  - Borderless window placed with `SetWindowPos` to exactly cover the target monitor's full physical bounds; per-monitor DPI aware
  - One `SkiaSharp.Views.WPF.SKElement` per widget on a WPF `Canvas`; each widget has its own `DispatcherTimer` driven by its refresh attribute (no global render loop)
  - Re-targets the monitor after display-settings changes, resume from sleep, and config changes
  - A widget that throws is drawn as a visible error tile
  - `--snapshot out.png [--size WxH]` renders the active page off-screen with the same layout/widget code

- **Dynamic Grid Layout Engine (in `UrDeck.Core`)**
  - Always 4 columns, columns/rows computed dynamically from monitor width/height
  - Grid cell = `ColumnWidth = screenWidth / 4`, `RowHeight = ColumnWidth` (square cells)
  - Widgets store their size in grid units (e.g., `[WidgetSize(4, 2)]`), layout engine converts to pixel positions
  - Supports any monitor resolution: 3840×2160, 7680×2160, 1100×3840, etc.
  - `GridLayoutManager` computes widget positions, clamps invalid placements with a logged warning

- **Clock Widget (`UrDeck.Widgets.Clock`, `net10.0` plugin DLL)**
  - Displays current time and date on a rounded panel card
  - Demonstrates the full plugin pipeline: metadata attributes → plugin loading → grid rendering → SkiaSharp drawing
  - `[Widget("Clock", ..., Id = "urdeck.widgets.clock")]`, sizes 4×2, 4×1, 2×1, 1×1, `[RefreshOnTick(1, TimeUnit.Seconds)]`

- **Configuration System**
  - JSON configuration file (`urdeck-config.json`) stored next to the executable
  - Stores pages (widget typeId → grid position/size + widget-specific settings), active page index, dock shortcuts, theme name, and target monitor (`monitorName`, `monitor`)
  - Loads config at startup, saves on exit, hot-reloads on file change

## Capabilities

### New Capabilities
- **`widget-sdk`**: Core widget contract, metadata attributes, plugin loader, refresh strategies, render context, and widget registry
- **`grid-layout`**: Resolution-agnostic 4-column grid layout engine with dynamic pixel-to-unit conversion
- **`host-shell`**: WPF host window with per-widget SkiaSharp elements, monitor targeting, plugin loading orchestration, and configuration management
- **`widget-clock`**: Built-in Clock widget proving the end-to-end pipeline

### Modified Capabilities
*(none — all new)*

## Impact

- **New Projects**: `UrDeck.Core` (widget SDK, layout, config, plugin loader), `UrDeck.Analyzer` (Roslyn), `UrDeck.Host` (WPF shell), `UrDeck.Widgets.Clock` (clock plugin), plus `UrDeck.Core.Tests` and `UrDeck.Analyzer.Tests`
- **Dependencies**: `SkiaSharp` 3.119.4, `SkiaSharp.Views.WPF` 3.119.4 (host only)
- **Removals**: None (clean slate)
- **Breaking Changes**: None (no existing codebase)
- **Post-Motion Considerations**: Widget plugins are compile-time .NET 10 assemblies — third-party developers reference `UrDeck.Core` (project reference or future NuGet package) with `Private="false"` and the analyzer
- **Follow-up changes**: `theme-engine` (shared style system) and `display-targeting` (monitor picker and robust display handling)
