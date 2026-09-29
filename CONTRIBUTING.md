# Contributing to Widgy

Thanks for helping. This document covers the workflow, code standards, how to write a widget and what reviewers check.
Agents and humans follow the same rules; the short version for agents is in [AGENTS.md](AGENTS.md).

## Requirements

- Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download) (`global.json` pins it loosely)
- Node.js, only for `openspec validate` (`npm i -g @fission-ai/openspec`)

## Workflow

1. Pick an item from [docs/ROADMAP.md](docs/ROADMAP.md) (or open an issue first). Larger changes get an OpenSpec change
   under `openspec/changes/` (proposal, design, specs, tasks) that passes `openspec validate --all --strict`.
2. Branch off `main`: `feat/short-name`, `fix/...`, `docs/...`, `chore/...`. Never commit to `main` directly.
3. Build, test and format locally (commands in [AGENTS.md](AGENTS.md)). Warnings are errors.
4. Push and open a pull request using the template. **The PR title must be a conventional commit**
   (`feat(host): pick the target monitor from a list`); types are `feat`, `fix`, `docs`, `test`, `perf`, `refactor`,
   `chore`, `ci`, `build`, `revert`.
5. CI must be green and conversations resolved. The PR is **squash-merged**: GitHub uses the PR title as the commit
   title and the PR description as the commit body, so keep the description tidy (delete the template comments).
6. The head branch is deleted on merge. Keep branches short-lived and update them from `main` when they fall behind.

`main` is protected by a ruleset: pull request required, required checks `build` and `pr-title`, linear history, no
force pushes or deletion, squash merges only.

## Code standards

- Enforced by `.editorconfig` and the build (`dotnet format widgy.slnx --verify-no-changes --severity warn` in CI):
  file-scoped namespaces, `using`s outside the namespace and sorted, `_camelCase` private instance fields, PascalCase
  static fields, `I` prefix for interfaces, `var` only when the type is apparent, LF line endings.
- Shared MSBuild settings are in `Directory.Build.props`; package versions are only in `Directory.Packages.props`.
- Tests: xUnit in `tests/`. `Widgy.Core` has a line-coverage floor (currently 65%, enforced in CI; raise it when
  coverage improves, never lower it to make a PR pass).
- Comments explain why, not what. Match the surrounding code.

## Writing a widget

A widget is a class deriving from `Widget<TConfig>` in a class library that references `Widgy.Core`. Attributes
provide the metadata, so you normally override only `Render`. `Render` is synchronous, runs on the UI thread and must
be fast; override `UpdateAsync` to fetch data off the render path.

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

Project file for a first-party widget (modelled on `widgets/Widgy.Widgets.Clock`; it inherits the shared settings from
`Directory.Build.props`). A third-party widget in its own repo sets `TargetFramework` and the other properties itself.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$(WidgyTfm)</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <!-- Widgy.Core and SkiaSharp come from the host at runtime; do not copy them next to the plugin. -->
    <ProjectReference Include="..\..\src\Widgy.Core\Widgy.Core.csproj" Private="false" />
    <ProjectReference Include="..\..\src\Widgy.Analyzer\Widgy.Analyzer.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <OutputItemType>Analyzer</OutputItemType>
    </ProjectReference>
  </ItemGroup>
</Project>
```

For a first-party widget also add a `ProjectReference` (with `ReferenceOutputAssembly=false`) and a copy step in
`src/Widgy.Host/Widgy.Host.csproj`, as the Clock has. Otherwise build the DLL and copy it into the host's `plugins/`
folder (next to `Widgy.Host.exe`). While Widgy is running, adding, replacing or deleting a DLL there reloads the plugins
and rebuilds the page after about half a second; the original file is never locked because plugins load from a shadow
copy in their own collectible `AssemblyLoadContext`. Reference the widget by its `Id` in `widgy-config.json`. A widget
that throws in `Render` shows a red error tile instead of taking the dashboard down.

## Performance budgets

Widgy's selling point is a low footprint, so cost is reviewed like correctness. The formal tiers and the benchmark mode
are roadmap item 4; until then, the working targets are:

- Host baseline: 60-70 MB private memory in Release (an empty WPF window alone is ~53 MB).
- A widget's dominant cost is its render surface (`width x height x 4` bytes); keep additional allocations small and
  avoid per-frame allocations in `Render`.
- Refresh as rarely as the content allows (the Clock needs a repaint once a minute, not every second).
- No blocking or slow work in `Render`; fetch in `UpdateAsync`.

State the measured impact under "Performance impact" in the PR description.

## Review checklist

- [ ] PR title is a conventional commit; description is written for `git log`
- [ ] Builds with 0 warnings; tests added or updated; format clean; CI green
- [ ] OpenSpec change linked/updated and tasks ticked only when verified
- [ ] No per-widget styling or resolution/scaling assumptions leaked in
- [ ] Performance impact measured or explicitly "none"
- [ ] Rendering checked with `--snapshot`, and on the real panel for host/layout/DPI changes
- [ ] Docs updated (README, ROADMAP status, AGENTS.md if commands or conventions changed)
