# Widgy

Widgy is a lightweight widget dashboard for a secondary or case display, built as a low-overhead replacement for HYTE Nexus. It was designed around the HYTE Y70 Touch panel (1100x3840, portrait) but adapts to any monitor: the layout is a 4-column grid of square cells computed from the monitor width.

It is a .NET 10 WPF application that draws each widget with SkiaSharp. Widgets are plain C# plugin DLLs that can be added, replaced or removed while Widgy is running.

## Status

Phase 1 (foundation) is implemented: widget SDK and Roslyn analyzer, hot-reloading plugin loader, grid layout, the WPF host with per-monitor DPI handling, JSON config with hot-reload, and a built-in Clock widget. Verified on the real 1100x3840 panel.

Not done yet: an editor UI, other widgets, the `[RefreshOnEvent]` event bus (such widgets render once), adaptive refresh scaling (`[RefreshAdaptive]` currently runs at its minimum interval), a theme engine and a monitor picker (both proposed under `openspec/changes/`). Memory use is currently about 139 MB working set, above the 50 MB goal.

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build, run, test

```powershell
dotnet build widgy.sln -c Release      # builds everything and copies the Clock plugin to plugins/
dotnet run --project Widgy.Host -c Release
dotnet test widgy.sln -c Release       # grid, config, plugin loader and analyzer tests
```

The window covers the target monitor completely. Press Esc to close it. Diagnostics go to `widgy.log` next to the executable.

### Snapshots

`--snapshot` renders the active page off-screen with the same layout and widget code as the live window, writes a PNG and exits (no window is shown):

```powershell
dotnet run --project Widgy.Host -c Release -- --snapshot out.png --size 1100x3840
```

`--size WxH` defaults to the target monitor's resolution. `*.snapshot.png` files are git-ignored.

## Configuration

Config is `widgy-config.json`, located next to the executable (`Widgy.Host/bin/<Configuration>/net10.0-windows10.0.19041.0/`). The tracked default lives at `Widgy.Host/widgy-config.json`; the build copies it to the output folder when the source is newer, so edit the copy next to the executable for live tweaks and the source file for new defaults. The file is hot-reloaded about a second after saving; an invalid edit is ignored and the last good config is kept. Missing files are created with defaults.

```json
{
  "pages": [
    {
      "name": "Default",
      "widgets": [
        {
          "typeId": "widgy.widgets.clock",
          "col": 0,
          "row": 0,
          "width": 4,
          "height": 2,
          "format": "24h",
          "showDate": true,
          "fontSize": 1.0
        }
      ]
    }
  ],
  "activePage": 0,
  "theme": "default-dark",
  "monitorName": "tallest"
}
```

| Field | Meaning |
|---|---|
| `pages[].widgets[]` | Widgets on a page. `typeId` is the widget's `Id`; `col` (0-3), `row`, `width` (1-4) and `height` are in grid units. Invalid values are clamped and a warning is logged. An unknown `typeId` leaves its cell empty. |
| any other property on a widget | Widget-specific settings, e.g. the Clock's `format` (`"24h"` or `"12h"`), `showDate`, `textColor` (hex) and `fontSize` (multiplier). |
| `activePage` | Index of the page to show. |
| `monitorName` | Target monitor: `"primary"`, `"tallest"` (tallest portrait monitor), `"widest"`, `"largest"` (most pixels), or a device name such as `"DISPLAY1"`. |
| `monitor` | 1-based monitor index, used when `monitorName` matches nothing. Falls back to the primary monitor. |
| `theme`, `dock` | Stored but not used yet. |

Grid math: `ColumnWidth = screenWidth / 4` and `RowHeight = ColumnWidth`, so on a 1100 px wide panel each cell is 275x275 and a 4x2 widget is 1100x550.

## Writing a widget

A widget is a class deriving from `Widget<TConfig>` in a class library that references `Widgy.Core`. Attributes provide the metadata, so you normally override only `Render`. `Render` is synchronous, runs on the UI thread and must be fast; override `UpdateAsync` to fetch data off the render path.

```csharp
using SkiaSharp;
using Widgy.Core;
using Widgy.Core.Attributes;
using Widgy.Core.Config;
using Widgy.Core.Enums;
using Widgy.Core.Rendering;

public class HelloConfig : WidgetConfig
{
    public string Text { get; set; } = "Hello, Widgy";
}

[Widget("Hello", "Draws a greeting", Id = "example.hello")]
[WidgetSize(2, 1)]
[WidgetSize(4, 1)]
[RefreshOnTick(1, TimeUnit.Minutes)]
[Category("Examples")]
public class HelloWidget : Widget<HelloConfig>
{
    public override void Render(WidgetRenderContext context)
    {
        using var font = new SKFont(SKTypeface.Default, context.PixelSize.Height * 0.3f);
        using var paint = new SKPaint { Color = context.Theme.TextColor, IsAntialias = true };
        context.Canvas.DrawText(Config.Text, context.PixelSize.Width / 2f, context.PixelSize.Height * 0.6f,
            SKTextAlign.Center, font, paint);
    }
}
```

Rules enforced at compile time by the analyzer and again by the loader:

- `[Widget]` is required; `Id` is the stable `typeId` used in the config (it defaults to `Name`, so set it explicitly).
- At least one `[WidgetSize(width, height)]` (width 1-4, height at least 1).
- Exactly one of `[RefreshOnTick]`, `[RefreshAdaptive]` or `[RefreshOnEvent]`.
- A public parameterless constructor.

Project file (modelled on `Widgy.Widgets.Clock`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <!-- Widgy.Core and SkiaSharp come from the host at runtime; do not copy them next to the plugin. -->
    <ProjectReference Include="..\Widgy.Core\Widgy.Core.csproj" Private="false" />
    <ProjectReference Include="..\Widgy.Analyzer\Widgy.Analyzer.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <OutputItemType>Analyzer</OutputItemType>
    </ProjectReference>
  </ItemGroup>
</Project>
```

Build it and copy the DLL into the host's `plugins/` folder (next to `Widgy.Host.exe`). While Widgy is running, adding, replacing or deleting a DLL there reloads the plugins and rebuilds the page after about half a second; the original file is never locked because plugins load from a shadow copy in their own collectible `AssemblyLoadContext`. Then reference the widget by its `Id` in `widgy-config.json`. A widget that throws in `Render` shows a red error tile instead of taking the dashboard down.

## Project layout

| Path | Purpose |
|---|---|
| `Widgy.Core` | Widget SDK (`net10.0`): attributes, `Widget<TConfig>`, `IWidget`, render context, config store, grid layout, plugin loader, `PageRenderer` |
| `Widgy.Analyzer` | Roslyn analyzer (`netstandard2.0`) reporting WIDGY001-005 |
| `Widgy.Host` | WPF application (`net10.0-windows10.0.19041.0`): window and monitor placement, one `SKElement` per widget |
| `Widgy.Widgets.Clock` | Built-in Clock plugin (`widgy.widgets.clock`) |
| `Widgy.Core.Tests`, `Widgy.Analyzer.Tests` | xUnit tests |
| `openspec/` | Spec-driven change documents: `widgy-framework` (Phase 1), plus the proposed `theme-engine` and `display-targeting` |

The Windows SDK suffix on the host and test target frameworks is required: `SkiaSharp.Views.WPF` only ships its .NET build for `net10.0-windows10.0.19041`.
