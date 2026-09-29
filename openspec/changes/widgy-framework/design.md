# Widgy Framework — Technical Design

## Context

The user wants a replacement for HYTE Nexus — a lightweight, beautiful, extensible widget dashboard for a dedicated secondary monitor (1100×3840 vertical touch panel). The workspace is completely empty. No legacy code constrains the architecture. The primary monitor may run games, IDEs, browsers, etc. simultaneously, so Widgy must consume minimal CPU and memory.

The target platform is Windows only, using .NET 8. The widget system must be extensible via plugin DLLs. Each widget controls its own refresh rate. The layout is a fixed 4-column grid that adapts to any monitor resolution.

## Goals / Non-Goals

**Goals:**
- Widget SDK with metadata-driven plugin system (data annotations + code analyzer)
- Dynamic 4-column grid layout engine that works at any resolution
- WPF host with SkiaSharp canvas for GPU-accelerated, visually stunning rendering
- Clock widget proving the full end-to-end pipeline
- Configuration system (JSON) for persisting layout, pages, themes
- Plugin hot-reload detection (file watcher on `plugins/` directory)

**Non-Goals:**
- Editor UI (drag-to-place, property grid, widget palette) — reserved for Phase 2
- Video background support — reserved for Phase 4
- Touch input handling on individual widgets — reserved for Phase 4
- Cross-platform support — Windows only
- Mobile companion app
- Widget store / discovery system

## Decisions

### Decision 1: Framework — WPF + SkiaSharp (not WPF/DirectX, not Avalonia, not Uno)

**Chosen:** WPF host with `SkiaSharp` and `SkiaSharp.Views.WPF` canvas surface.

**Rationale:**
- WPF provides the window management, plug-in loading (via `System.Reflection`), and configuration infrastructure with ~50MB baseline memory.
- SkiaSharp provides the 2D canvas for all widget rendering with minimal memory overhead (~30MB additional) and excellent visual quality (gradients, anti-aliasing, text rendering, circles/gauges).
- `SkiaSharp.Views.WPF` provides a WPF-compatible canvas control that renders directly to the GPU — no rendering bridge overhead.
- Alternative WPF/DirectX (D2D1W) adds complexity without benefit for 2D widget rendering.
- Alternative Avalonia adds an unnecessary abstraction layer and ~10-20MB more memory.
- Alternative Uno/WASM is overkill for Windows-only with larger startup footprint.

### Decision 2: Widget Metadata — Data Annotations + Code Analyzer (not pure interface, not config-file driven)

**Chosen:** C# data attributes + Roslyn analyzer for compile-time validation.

**Rationale:**
- Data annotations provide the simplest authoring experience:
  ```csharp
  [Widget("Clock", "Display current time")]
  [WidgetSize(4, 2)]
  [RefreshOnTick(1, TimeUnit.Seconds)]
  public class ClockWidget : Widget<ClockConfig> { }
  ```
- Roslyn analyzer catches errors at compile time (no runtime reflection failures).
- Alternative: Pure interface-based (e.g., `IWidget.Config`) works but requires more code per widget — worse ergonomics.
- Alternative: Config-file driven (e.g., `widget.json`) adds serialization complexity and runtime error paths.

### Decision 3: Refresh Strategy — Per-widget attribute (not global, not hardcoded)

**Chosen:** Each widget declares its own refresh policy via attributes:
- `[RefreshOnTick(seconds)]` — fire render callback every N seconds (Clock: 1s, Weather: 300s)
- `[RefreshAdaptive(minMs, maxMs)]` — smart throttle based on system load (charts, graphs)
- `[RefreshOnEvent("eventKey")]` — event-driven, only renders when data arrives (PerfMonitor)

**Rationale:** Different widgets have fundamentally different data update patterns. Forcing all widgets into a global update loop would waste CPU on static widgets (clock at 60fps = unnecessary). Per-widget policies allow the runtime to only schedule timer subscriptions for widgets that actually need them.

### Decision 4: Grid Layout — Resolution-agnostic grid units (not pixel-based, not percentage-based)

**Chosen:** Widget size in grid units (1-4 columns, 1-N rows). The layout engine converts to pixels at render time based on the current monitor resolution:
```csharp
GridLayoutManager(screenWidth, screenHeight)
  ColumnWidth = screenWidth / 4;
  RowHeight = ColumnWidth; // Square cells
  PixelPosition(widget) = (widget.Col * ColumnWidth, widget.Row * RowHeight);
```

**Rationale:**
- Grid units make widgets resolution-independent: a 4×2 widget always fills the full panel width and occupies 2 rows of display data, regardless of whether the panel is 1100px wide or 1920px wide.
- Pixel-based sizes would need hardcoded resolution presets or complex DPI scaling.
- Percentage-based sizes work but grid units are more intuitive for widget authors: "this widget takes 4 columns" maps directly to the visual layout.

### Decision 5: Plugin Loading — Assembly.LoadFrom from `plugins/` directory (not reflection-only, not MSBuild)

**Chosen:** At startup (and on file change):
```csharp
foreach (var dll in Directory.EnumerateFiles("plugins", "*.dll"))
{
    var assembly = Assembly.LoadFrom(dll);
    foreach (var type in assembly.GetTypes())
    {
        if (type.IsAbstract) continue;
        var widgetAttr = (WidgetAttribute?)type.GetCustomAttribute(typeof(WidgetAttribute));
        if (widgetAttr != null)
        {
            registry.Register(widgetAttr, type);
        }
    }
}
```

**Rationale:** Simple, no external dependencies, fast (<100ms typically). `Assembly.LoadFrom` avoids GAC conflicts. File watcher (`FileSystemWatcher`) enables hot-reload during development.

### Decision 6: Configuration — JSON with hot-reload (not XML, not binary)

**Chosen:** `widgy-config.json` in the application directory (or `~/.widgy/` on Windows). JSON is human-editable, well-supported in .NET 8 via `System.Text.Json`, and supports hot-reload via file watcher.

**Schema:**
```json
{
  "pages": [
    {
      "name": "Primary",
      "widgets": [
        {
          "type": "widgy.widgets.clock",
          "col": 0,
          "row": 0,
          "width": 4,
          "height": 2
        }
      ],
      "background": { "type": "static", "path": "" }
    }
  ],
  "activePage": 0,
  "dock": [
    { "type": "app", "command": "explorer.exe" },
    { "type": "url", "url": "https://github.com" }
  ],
  "theme": "default-dark"
}
```

### Decision 7: Performance Model — Idle = Zero CPU (never tick everything)

**Chosen:** The runtime only schedules timers for widgets that declare `[RefreshOnTick]`. Widgets that declare `[RefreshOnEvent]` listen on a `Channel<T>` or `Action` delegate. The Clock widget subscribes to a 1-second timer; the Weather widget (if present) subscribes to a 5-minute timer. No global update loop exists.

```
┌───────────┐   Timer subscription per widget type:
│  Runtime   │
│  Scheduler │   → Clock: 1s timer → Clock.Render()
│            │   → Weather: 5m timer → Weather.Render()
│   Timer    │   → PerfMonitor: Channel<float> → Perf.Render()
│   Manager  │   → MediaControl: 0.5s timer
└───────────┘
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| SkiaSharp at 1100×3840 may be heavy on GPU for complex widgets | SkiaSharp is highly optimized for 2D; gauge charts render in <0.5ms. Monitor with simple benchmarks. |
| Assembly.LoadFrom can cause loading context issues in .NET | Use `AssemblyLoadContext` for sandboxing plugins, or stick to `Assembly.LoadFrom` which is standard for this pattern in .NET Framework/WPF apps. |
| Grid layout with square cells may look odd on non-square monitors | RowHeight = ColumnWidth is the natural choice for a 4-column grid. On a 1100px-wide panel, ColumnWidth = 275px, RowHeight = 275px. This is exactly what the user showed in their design. |
| Hot-reload via FileSystemWatcher may miss rapid saves | Debounce file events with a 500ms delay. On the first save, the widget reloads; subsequent saves within 500ms coalesce. |
| JSON config with hundreds of widgets may be slow to parse | Use `System.Text.Json` with `Utf8JsonReader` streaming for large configs. Typical configs have <50 widgets, so this is rarely a concern. |

## Migration Plan

No migration plan needed — this is a fresh codebase. The build process is:
1. Build `Widgy.Core` (widget SDK)
2. Build `Widgy.Analyzer` (Roslyn)
3. Build `Widgy.Host` (WPF shell, depends on Core)
4. Build `Widgy.Widgets.Clock` (clock plugin, depends on Core)
5. Copy plugin DLL to `Widgy.Host/bin/Debug/net8.0-windows/plugins/` at build time
6. Run `Widgy.Host`

## Open Questions

1. **Touch support timing**: Will touch input work on the WPF window directly, or do individual widgets need touch handlers? (Answer: Phase 4 — deferred)
2. **Font rendering for 4K displays**: Should we use `TextRenderType.GdiClassic` or `TextRenderType.ClearType`? (Answer: We'll use default WPF text rendering — it's already ClearType by default. Test at 1100px width.)
3. **Plugin signing**: Should widgets be digitally signed? (Answer: Phase 3 — deferred. No signing for initial implementation.)
