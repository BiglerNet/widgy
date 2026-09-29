# Widgy Framework — Technical Design

## Context

The user wants a replacement for HYTE Nexus — a lightweight, beautiful, extensible widget dashboard for a dedicated secondary monitor (1100×3840 vertical touch panel). The primary monitor may run games, IDEs, browsers, etc. simultaneously, so Widgy must consume minimal CPU and memory.

The target platform is Windows only, using .NET 10. The widget system is extensible via plugin DLLs. Each widget controls its own refresh rate. The layout is a fixed 4-column grid that adapts to any monitor resolution.

This document was synced with the implementation after the first working build; where the code diverged from the original plan, the code is the source of truth and the decision text below reflects it.

## Goals / Non-Goals

**Goals:**
- Widget SDK with metadata-driven plugin system (data annotations + code analyzer + runtime validation)
- Dynamic 4-column grid layout engine that works at any resolution
- WPF host with SkiaSharp rendering for high-quality 2D widgets
- Clock widget proving the full end-to-end pipeline
- Configuration system (JSON) for persisting layout, pages, themes, target monitor
- Plugin and config hot-reload
- Memory footprint under 50MB (open — see Risks)

**Non-Goals:**
- Editor UI (drag-to-place, property grid, widget palette) — reserved for Phase 2
- Video background support — reserved for Phase 4
- Touch input handling on individual widgets — reserved for Phase 4
- Cross-platform support — Windows only
- Mobile companion app
- Widget store / discovery system

## Decisions

### Decision 0: Runtime and target frameworks — .NET 10

| Project | TargetFramework | Why |
|---|---|---|
| `Widgy.Core`, `Widgy.Widgets.*` plugins | `net10.0` | The SDK has no Windows dependency; only SkiaSharp. |
| `Widgy.Host` | `net10.0-windows10.0.19041.0` | The Windows SDK version suffix is required: `SkiaSharp.Views.WPF` 3.119.4 only ships its .NET build for that TFM. Plain `net10.0-windows` silently falls back to the `net48` build of the package. |
| `Widgy.Analyzer` | `netstandard2.0` | Roslyn analyzers must target netstandard2.0. |
| Test projects | `net10.0-windows10.0.19041.0` | Same TFM as the host. |

SkiaSharp and SkiaSharp.Views.WPF are pinned to 3.119.4.

### Decision 1: Framework — WPF + SkiaSharp, one `SKElement` per widget

**Chosen:** WPF host; each widget gets its own `SkiaSharp.Views.WPF.SKElement` placed on a WPF `Canvas` at its grid rectangle. There is no page-level canvas.

**Rationale:**
- WPF provides window management and per-monitor DPI handling.
- SkiaSharp provides the 2D canvas for widget rendering (gradients, anti-aliasing, text, gauges).
- `SKElement` rasterizes in software into a `WriteableBitmap` at the monitor's physical resolution (it is not GPU-accelerated; the original "GPU-accelerated" claim was wrong for `SKElement`). Because each widget owns its element, a widget cannot draw outside its cell, and an idle widget costs nothing between its own timer ticks.
- Alternative WPF/DirectX adds complexity without benefit for 2D widget rendering. Avalonia and Uno were rejected as unnecessary abstraction/footprint.

### Decision 2: Widget contract — non-generic `IWidget` + `IWidget<TConfig>` + `Widget<TConfig>` base

**Chosen:**

```csharp
public interface IWidget
{
    string Name { get; }  string Description { get; }  string Category { get; }
    Size[] SupportedSizes { get; }
    Type ConfigType { get; }
    WidgetConfig Config { get; }
    void Configure(WidgetConfig config);
    Task UpdateAsync(CancellationToken ct);   // data refresh, off the render path
    void Render(WidgetRenderContext context); // synchronous, UI thread, must be fast
}
public interface IWidget<TConfig> : IWidget { new TConfig Config { get; } TConfig DefaultConfig { get; } }
```

Authors derive from `Widget<TConfig>`, whose `Name`/`Description`/`Category`/`SupportedSizes` default from the attributes, so a widget typically overrides only `Render`.

**Rationale:** The host must drive widgets of unknown config types, so it talks to a non-generic interface; the generic layer gives authors typed config. Splitting `UpdateAsync` (may do I/O) from `Render` (synchronous, canvas valid only during the call) keeps slow data fetches off the UI thread and rendering deterministic. The earlier `RenderAsync(SKCanvas, ...)` design was dropped: an async render cannot safely hold a canvas that is only valid during the paint callback.

### Decision 3: Widget metadata — Data annotations + Code Analyzer + runtime descriptor validation

**Chosen:** C# attributes, validated at compile time by a Roslyn analyzer (WIDGY001–005) and again at load time by the plugin loader.

```csharp
[Widget("Clock", "Display current time", Id = "widgy.widgets.clock")]
[WidgetSize(4, 2)]
[RefreshOnTick(1, TimeUnit.Seconds)]
public class ClockWidget : Widget<ClockConfig> { ... }
```

- `Id` is the stable type id used as `typeId` in `widgy-config.json`; it falls back to `Name` when omitted. Authors should set it explicitly.
- The loader builds a `WidgetDescriptor` per type (id, metadata, config type, sizes, refresh strategy + interval) and **rejects** types missing `[Widget]`, missing `[WidgetSize]`, having a number of refresh strategies other than exactly one, lacking a public parameterless constructor, or whose config type cannot be determined. Rejections are logged and do not stop other plugins from loading.

**Rationale:** Compile-time errors give the best authoring feedback; the runtime check protects the host from plugins built without the analyzer.

### Decision 4: Refresh strategy — per-widget attribute, per-widget timer

Each widget declares its refresh policy via attributes:
- `[RefreshOnTick(interval, unit)]` — implemented. The host starts a `DispatcherTimer` with that interval for the widget's `SKElement`; each tick runs `UpdateAsync` then invalidates the element.
- `[RefreshAdaptive(minMs, maxMs)]` — **partially implemented**: currently behaves like a tick at `MinMs`. Scaling between `MinMs` and `MaxMs` based on load is TODO (tracked in tasks.md).
- `[RefreshOnEvent("eventKey")]` — **not implemented yet**: the widget is rendered once on load; there is no event channel/bus, so nothing re-triggers it. Tracked in tasks.md.

There is no global render loop: each widget's `SKElement` repaints only on its own timer (or on resize/expose), so an idle dashboard costs ~0 CPU (measured ~0.03% idle).

### Decision 5: Grid layout — resolution-agnostic grid units

**Chosen:** Widget size in grid units (1–4 columns, 1–N rows). `GridLayoutManager` converts to pixels:
```
ColumnWidth = screenWidth / 4
RowHeight   = ColumnWidth          // square cells
Pixel(x, y) = (col * ColumnWidth, row * RowHeight)
Pixel(w, h) = (width * ColumnWidth, height * RowHeight)
```
Layout is computed in device-independent pixels (DIPs) from the window size; each `SKElement` then renders at the monitor's physical resolution.

**Clamping:** width is clamped to 1–4 first, then col to `0..(4 - width)`, row to ≥ 0, height to ≥ 1; a warning is logged when anything is clamped.

**Rationale:** Grid units make widgets resolution-independent: a 4×2 widget fills the panel width and is two square cells tall whether the panel is 1100px or 1920px wide. Because the row height is derived from the width, tall panels have many rows and wide/short monitors have few.

### Decision 6: Plugin loading — collectible `AssemblyLoadContext` with shadow copies

**Chosen:** Each plugin DLL is copied to `%TEMP%/widgy-shadow/<pid>/<guid>/` (with `.pdb`/`.deps.json` siblings) and loaded from that copy into its own collectible `AssemblyLoadContext` (`PluginLoadContext`). `Widgy.Core`, SkiaSharp and the framework resolve from the default context, so `IWidget`/`WidgetConfig` type identity is shared between host and plugin; a plugin's own dependencies resolve from the plugin folder.

The `plugins/` directory is watched (`FileSystemWatcher`, 500ms debounce). On change the loader unloads all contexts, clears the registry, reloads, and raises `PluginsChanged`; the host rebuilds the page. Stale shadow directories from dead processes are cleaned at startup. The built-in Clock plugin is copied to `plugins/` by the host build (an MSBuild target), so it is discovered like any third-party plugin.

**Rationale:** `Assembly.LoadFrom` (the original plan) locks the DLL so it cannot be rebuilt while Widgy runs, and can never be unloaded or replaced. A collectible ALC plus shadow copy makes plugin development a live loop and lets a plugin be removed or restored while running.

**WPF pinning (resolved):** WPF's `MS.Internal.*.SafeSecurityHelper` keeps a static, never-evicted assembly cache that rooted collectible plugin assemblies (found via `gcroot` on the plugin's `LoaderAllocator`). On `PluginsChanged` the host disposes widget views and evicts collectible assemblies from those caches (`src/Widgy.Host/WpfAssemblyCache.cs`, best-effort reflection on WPF internals). `WidgetConfig.ToConcrete` uses a private System.Text.Json resolver so the shared options cache doesn't pin plugin config types, and the loader nudges STJ's ~1s accessor cache before checking collection. Verified live: the old context is collected ~1.9s after unload.

### Decision 7: Configuration — JSON with hot-reload, widget settings as extra properties

**Chosen:** `widgy-config.json` next to the executable. Widget-specific settings are extra properties on the widget's JSON object (e.g. `"format": "12h"`), captured by `[JsonExtensionData]` on `WidgetConfig` (so they round-trip on save) and converted to the widget's concrete config type (e.g. `ClockConfig`) when the widget is created.

```json
{
  "pages": [
    { "name": "Default",
      "widgets": [
        { "typeId": "widgy.widgets.clock", "col": 0, "row": 0, "width": 4, "height": 2,
          "format": "24h", "showDate": true, "fontSize": 1.0 }
      ] }
  ],
  "activePage": 0,
  "dock": [],
  "theme": "default-dark",
  "monitorName": "tallest"
}
```

`monitorName` is `"primary" | "tallest" | "widest" | "largest"` or a device name such as `"DISPLAY1"`; a 1-based `monitor` index is the fallback when the name matches nothing, and primary is the final fallback.

`ConfigStore` reloads on file change (1s debounce), retries when the file is locked by an editor, keeps the last good config when the new file fails to parse, ignores its own saves (2s window), and saves on exit. `dock`, `theme` and `background` are persisted but not yet consumed by the host.

### Decision 8: Window and monitor handling — physical pixels, exact cover

- The process is PerMonitorV2 DPI aware (`app.manifest`).
- Monitors are enumerated with Win32 (`EnumDisplayMonitors`/`GetMonitorInfo`) in physical pixels.
- The window is positioned with `SetWindowPos` to exactly cover the target monitor's **full bounds** (not the work area), and re-applied after `DpiChanged` because WPF rescales the window when it crosses monitors of different DPI.
- The host re-selects and re-covers the target on display-settings changes, on resume from sleep (re-checking after 1.5s and 5s, because monitors wake in arbitrary order), and on config changes. Layout rebuilds are coalesced.
- Escape closes the window.

When the configured monitor is absent the host currently just falls back to the primary monitor; a proper story is in the `display-targeting` change.

### Decision 9: Fault isolation and diagnostics

- A widget whose `Render` throws is drawn as a visible error tile (`WidgetPainter.RenderSafely`) instead of blanking or crashing the host; an `UpdateAsync` failure is logged and rendering continues.
- All diagnostics go through `WidgyLog` (file `widgy.log` next to the executable plus debug output); there are no `Console.Write` calls. (`GridLayoutManager` still emits its clamp warning via `Trace.TraceWarning`, so it does not reach `widgy.log`.)
- `--snapshot out.png [--size WxH]` renders the active page off-screen through `PageRenderer` using the same layout and widget code, defaulting to the target monitor's resolution; it exists for verification and docs.

### Decision 10: Widget styling belongs in a theme engine (future)

The Clock currently draws a rounded "panel card" (`ThemeColors.PanelBackgroundColor`, small inset/radius) behind its text, and picks its own fonts (Segoe UI Semibold time, Segoe UI Light date at ~80% alpha). The user approved the card over the earlier "transparent background" wording. Styling decisions like this should not live in each widget: they should move into a shared theme/style engine that widgets consume through `WidgetRenderContext` (proposed as the `theme-engine` change).

### Decision 11: Performance model — idle = near-zero CPU

The runtime only starts timers for widgets that declare a timer-based refresh. No global update loop exists. Measured idle CPU is ~0.03%.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| **Memory goal (< 50MB) is not met.** Release with software-only WPF composition (now the default; `WIDGY_HWRENDER=1` opts out) measures ~66 MB private / ~115 MB working set with one Clock; an empty WPF window is ~53 MB private. GC/JIT/ReadyToRun settings measured as no-ops. See `docs/perf/memory-investigation.md`. | Open decision: restate the goal for a WPF host (~65 MB private) or move rendering to a plain Win32 window + Skia (est. 20-30 MB, unmeasured). |
| WPF internals pin collectible plugin assemblies | Worked around via `WpfAssemblyCache` (reflection on private WPF fields). If a future WPF update renames them, the purge logs a warning and hot-reloaded plugin versions stay in memory until restart (functionally still correct). |
| `SKElement` is software-rasterized at physical resolution; many large widgets at 1100×3840 could get heavy | Widgets repaint only on their own timer; measure before adding animated widgets. A GPU-backed surface could be revisited. |
| Grid with square cells may look odd on non-square monitors | RowHeight = ColumnWidth is the natural choice for a 4-column grid; verified on the 1100×3840 panel (275px cells) and after retargeting to a 1080×1920 monitor. |
| Hot-reload via `FileSystemWatcher` may miss or duplicate rapid saves | Debounce (500ms plugins, 1s config); config keeps last good state on parse errors. |
| Monitor identity by `DISPLAYn` device name is unstable across re-plugs/reboots | Addressed in the `display-targeting` change. |
| JSON config with hundreds of widgets may be slow to parse | Typical configs have < 50 widgets; not a concern in practice. |

## Build and Layout

1. `dotnet build widgy.slnx -c Release` builds everything; the host build copies the Clock plugin DLL to `src/Widgy.Host/bin/<cfg>/<tfm>/plugins/`.
2. The source default config is `src/Widgy.Host/widgy-config.json` (tracked in the repository); the build copies it (PreserveNewest) next to the executable, and that runtime copy under `bin/` is ignored by version control.
3. Run `Widgy.Host`.

## Open Questions

1. **Touch support timing**: Will touch input work on the WPF window directly, or do individual widgets need touch handlers? (Deferred to Phase 4.)
2. **Font rendering**: Skia text is rendered with grayscale/subpixel antialiasing into the element's bitmap (not WPF ClearType). Acceptable at the panel's density; revisit if fringing is visible.
3. **Plugin signing**: Should widgets be digitally signed? (Deferred to Phase 3; none for now.)
4. **Memory**: How close to the 50MB goal can a WPF host get? (Open; see Risks.)
5. **Event bus**: Design of the channel behind `[RefreshOnEvent]` (who publishes, threading, lifetime across plugin reload). (Open; tracked in tasks.md.)
6. **Adaptive refresh**: What "system load" signal drives `[RefreshAdaptive]`? (Open; tracked in tasks.md.)
