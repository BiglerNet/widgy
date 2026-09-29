# Widgy Framework — Phase 1: Foundation (Hello World)

## Why

HYTE Nexus is a popular PC companion display app that fills a clear market need — a customizable widget dashboard for a secondary monitor — but it is an Electron-based application that consumes excessive memory, CPU, and disk space. The user owns a custom PC case with a vertical 1100×3840 touch panel display and wants a lightweight, beautiful replacement that:

1. Runs with minimal resource consumption (priority #1): idle widgets should consume near-zero CPU; total memory footprint should stay under 50MB.
2. Looks exceptional (priority #2): custom rendering with smooth animations, gradient support, and visually stunning gauge/chart widgets.
3. Is fully extensible: users and third-party developers should be able to create new widgets by writing a single C# plugin DLL.
4. Supports any monitor resolution dynamically: the 4-column grid layout must automatically adapt to any screen size and DPI.
5. Targets Windows only, using the proven .NET 8 runtime.

This proposal establishes the foundation layer: the widget SDK (metadata-driven widget contract and plugin loading system), the dynamic grid layout engine, and the WPF/SkiaSharp host shell — culminating in a running application that displays a single Clock widget on a 4-column grid on any monitor.

## What Changes

- **New Widget SDK (C# .NET 8 library)**
  - Data annotation attributes for widget metadata (`[Widget]`, `[WidgetSize]`, `[RefreshOnTick]`, `[Category]`, `[WidgetConfig]`)
  - Interface definition (`IWidget<TConfig>`) that all widgets must implement
  - Widget configuration model (`WidgetConfig` base class)
  - Refresh policy attributes: `[RefreshOnTick]`, `[RefreshAdaptive]`, `[RefreshOnEvent]`
  - Runtime context model (`WidgetRenderContext`) passed to widgets during render

- **New Roslyn Code Analyzer (C# analyzer package)**
  - Validates widget attributes at compile time: requires `[Widget]`, requires at least one `[WidgetSize]`, requires one refresh strategy, validates attribute consistency
  - Emits compile-time errors (not warnings) for missing/invalid attributes
  - Zero runtime overhead: runs only during builds

- **Plugin Loader (C# .NET 8 library)**
  - Scans a `plugins/` directory at startup for `*.dll` files
  - Uses `Assembly.LoadFrom()` to load each plugin assembly
  - Finds all types implementing `IWidget<TConfig>` via reflection (attributes + interface)
  - Extracts metadata from attributes and registers widgets in a `WidgetRegistry`
  - Supports hot-reload detection: monitors directory for file changes and reloads changed plugins
  - Per-widget instance lifecycle: creates instances per-page per-widget-type at runtime

- **WPF Host Shell (WPF .NET 8 app)**
  - Primary window bound to target monitor dimensions at startup
  - `SkiaSharp` canvas surface for GPU-accelerated 2D rendering of all widget content
  - Window style: child of taskbar, normal Z-order (runs beneath other windows on the monitor), no transparency, solid rendering
  - Monitors monitor connection/disconnection and resizes window dynamically

- **Dynamic Grid Layout Engine (C# .NET 8 library)**
  - Always 4 columns, columns/rows computed dynamically from monitor width/height
  - Grid cell = `ColumnWidth = screenWidth / 4`, `RowHeight = ColumnWidth` (square cells)
  - Widgets store their size in grid units (e.g., `[WidgetSize(4, 2)]`), layout engine converts to pixel positions at render time
  - Supports any monitor resolution: 3840×2160, 7680×2160, 1100×3840, etc.
  - Layout manager class (`GridLayoutManager`) that computes widget positions, validates constraints, and produces a grid model each frame

- **Clock Widget (C# .NET 8 plugin DLL)**
  - Displays current time and date on the grid
  - Demonstrates the full plugin pipeline: metadata attributes → plugin loading → grid rendering → SkiaSharp canvas drawing
  - Uses `[WidgetSize(4, 2)]` and `[RefreshOnTick(1, TimeUnit.Seconds)]`
  - Clean, modern digital clock rendering with optional date line

- **Configuration System**
  - JSON configuration file (`widgy-config.json`) stored in application directory
  - Stores page layout (widget ID → grid position/size), active page index, dock shortcuts, theme selection
  - Loads config at startup, saves on close, hot-reloads on file change

## Capabilities

### New Capabilities
- **`widget-sdk`**: Core widget contract, metadata attributes, plugin loader, refresh strategies, render context, and widget registry
- **`grid-layout`**: Resolution-agnostic 4-column grid layout engine with dynamic pixel-to-unit conversion
- **`host-shell`**: WPF host window with SkiaSharp canvas, monitor detection, plugin loading orchestration, and configuration management
- **`widget-clock`**: Built-in Clock widget proving the end-to-end pipeline

### Modified Capabilities
*(none — all new)*

## Impact

- **New Projects**: `Widgy.Core` (widget SDK), `Widgy.Analyzer` (Roslyn), `Widgy.Host` (WPF shell), `Widgy.Widgets.Clock` (clock plugin)
- **Dependencies**: `SkiaSharp`, `SkiaSharp.Views.WPF`
- **Removals**: None (clean slate)
- **Breaking Changes**: None (no existing codebase)
- **Post-Motion Considerations**: Widget plugins are compile-time .NET 8 assemblies — third-party developers need the `Widgy.Core` NuGet package or source reference
