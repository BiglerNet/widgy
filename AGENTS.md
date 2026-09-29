# Agent and contributor guide

UrDeck is a lightweight widget dashboard for secondary/case displays (WPF host, SkiaSharp rendering, plugin widgets).
Read `README.md` for what it is, `docs/ROADMAP.md` for what to work on, and `CONTRIBUTING.md` for the full process.

## Workflow (non-negotiable)

- **Never commit or push to `main`.** Branch off it (`feat/...`, `fix/...`, `docs/...`, `chore/...`), push, open a pull
  request. `main` is protected: PRs need green CI and are **squash-merged** only.
- The PR title is a conventional commit (`feat(scope): ...`; types `feat fix docs test perf refactor chore ci build
  revert`). It becomes the squash commit title and the PR description becomes the commit body, so write both well.
- One roadmap item / OpenSpec change per PR where possible. Behavior changes go through `openspec/changes/`.
- Tick `tasks.md` items only when verified; archive the change when all tasks are done.

## Commands

```powershell
dotnet build urdeck.slnx -c Release                     # 0 warnings required; warnings are errors
dotnet test urdeck.slnx -c Release
dotnet test tests/UrDeck.Core.Tests -c Release -p:CollectCoverage=true   # enforces the UrDeck.Core line-coverage floor
dotnet format urdeck.slnx --severity warn               # apply style; CI runs it with --verify-no-changes
openspec validate --all --strict
```

Render without a screen (from `src/UrDeck.Host/bin/Release/net10.0-windows10.0.19041.0/`):
`UrDeck.Host.exe --snapshot out.snapshot.png --size 1100x3840`.

CI (`.github/workflows/ci.yml`) runs all of the above on `windows-latest`; run them locally before pushing.

## Layout

```
src/UrDeck.Core       SDK: attributes, Widget<T>, render context, config, grid, plugin loader, PageRenderer
src/UrDeck.Analyzer   Roslyn analyzer (URDECK001-005), netstandard2.0
src/UrDeck.Host       WPF app: window/monitor placement, one SKElement per widget
widgets/             first-party widget plugins (UrDeck.Widgets.Clock, ...); copied to plugins/ by the host build
tests/               xUnit projects
docs/                ROADMAP.md, perf/, architecture notes
openspec/            spec-driven change documents
```

Shared build settings live in `Directory.Build.props`; package versions only in `Directory.Packages.props`
(no `Version=` on `PackageReference`). Style is in `.editorconfig`: file-scoped namespaces, `_camelCase` private
instance fields, PascalCase static fields, `var` only when the type is apparent, LF line endings. Every `.cs` file
needs the SPDX license header (`dotnet format` adds it): GPL-3.0-or-later everywhere except `src/UrDeck.Analyzer` (MIT).
See the README license map before moving code between projects: it can change the license.

## Windows / WPF gotchas

- Host and test projects must target `net10.0-windows10.0.19041.0` (`$(UrDeckWindowsTfm)`): `SkiaSharp.Views.WPF` only
  ships its .NET build for that Windows SDK version; plain `net10.0-windows` silently falls back to net48.
- Launching `UrDeck.Host.exe` (not `--snapshot`) blocks the shell. Start it detached and read `urdeck.log` next to the exe;
  it logs monitors, target vs. actual window bounds, placed widgets, reloads and warnings.
- Screen capture: use Windows PowerShell 5.1 (`powershell.exe`, not `pwsh`), call `SetProcessDpiAwarenessContext(-4)`
  first, then `Graphics.CopyFromScreen` on the monitor bounds. `PrintWindow` on the WPF window returns blank. Prefer one
  capture, or ask the user to look.
- In Git Bash, MSYS rewrites `/p:Foo` style switches as paths; use `-p:Foo`.
- Plugins load from a shadow copy in a collectible `AssemblyLoadContext`. WPF pins assemblies in internal caches
  (see `src/UrDeck.Host/WpfAssemblyCache.cs`); keep plugin types out of long-lived static caches.
- Do not add per-widget styling or resolution assumptions: styling belongs in the (planned) theme engine and the user
  never sees resolution or scaling.
