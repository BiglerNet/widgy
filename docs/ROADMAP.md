# UrDeck Roadmap

UrDeck is a lightweight, extensible widget dashboard for secondary/case displays (first target: the HYTE Y70 Touch
1100x3840 portrait panel), replacing HYTE Nexus. Priorities: low resource use, great visuals, a modular widget SDK,
a WYSIWYG editor, and touch.

This document is the hand-off point for work sessions. Each item below should become (or already is) an OpenSpec
change under `openspec/changes/`. Work one item per session.

## How to run a work session

1. Read `README.md`, this file, and the item's OpenSpec change (create it with the OpenSpec propose workflow if
   it doesn't exist yet; `openspec validate --all --strict` must pass).
2. Implement against `tasks.md`, ticking tasks only when verified.
3. Verify:
   - `dotnet build urdeck.slnx` (0 warnings) and `dotnet test urdeck.slnx`. `AGENTS.md` has the full command list.
   - Rendering without a screen: `UrDeck.Host.exe --snapshot out.png --size 1100x3840` (from the host's bin folder).
   - On the real panel: launch detached (the app otherwise blocks the shell), read `urdeck.log` next to the exe.
     It logs monitors, actual vs. target window bounds, placed widgets, reloads and warnings.
   - Screen capture on Windows: use Windows PowerShell 5.1 (`powershell.exe`, not `pwsh`), call
     `SetProcessDpiAwarenessContext(-4)` first, then `Graphics.CopyFromScreen` of the monitor bounds.
     `PrintWindow` on the WPF window returns blank. Prefer one capture, or ask the user to look.
4. Never commit to `main`. Branch off it (`feat/...`, `fix/...`, `docs/...`, `chore/...`), push, and open a pull
   request; PRs are squash-merged once CI is green. The PR title is a conventional commit
   (`feat(scope): ...`, `fix`, `docs`, `test`, `perf`, `refactor`, `chore`) and the PR description becomes the body of
   the squashed commit, so write both for the changelog reader. See "Git workflow" in item 1.
5. Archive the OpenSpec change when all its tasks are done.

Suggested model per item is noted as **Model**. Items marked Opus involve architecture decisions or hard debugging;
everything else should be fine on Sonnet.

## Current state (2026-09-29)

- `urdeck-framework` change (Phase 1 foundation) is nearly complete: SDK, analyzer, plugin loader with hot-reload
  (collectible AssemblyLoadContext), grid layout, WPF host with per-monitor DPI placement, Clock widget, tests.
- Proposed, not started: `theme-engine`, `display-targeting`.
- Memory: ~66 MB private / ~115 MB working set (Release, one Clock, software WPF composition). Idle CPU negligible.
  See `docs/perf/memory-investigation.md`.

---

## 1. Repository structure and standards

**Why:** the repo grew organically; contributors (human and agent) need a predictable layout and enforced style.
**Model:** Sonnet.
**Status:** done except the items marked open below (license, placeholder icon).

Layout (done; monorepo; first-party widgets live here, community widgets in their own repos):

```
src/
  UrDeck.Core/            # SDK (becomes the UrDeck.Sdk NuGet package later)
  UrDeck.Analyzer/
  UrDeck.Host/
widgets/                 # first-party widget plugins (UrDeck.Widgets.Clock, ...)
tests/
  UrDeck.Core.Tests/
  UrDeck.Analyzer.Tests/
docs/                    # ROADMAP.md, perf/, architecture notes
openspec/
```

Tasks:
- [x] Move projects; the host build still copies first-party widgets into `plugins/`. Solution is now `urdeck.slnx`.
- [x] `Directory.Build.props`: shared TFMs, `Nullable`, `ImplicitUsings`, `LangVersion`, `TreatWarningsAsErrors`,
  `EnforceCodeStyleInBuild`. `Directory.Packages.props` for central package versions (SkiaSharp 3.119.4, xunit, Roslyn).
- [x] `.editorconfig`: file-scoped namespaces (convert existing block namespaces), `var` usage, naming (`_camelCase`
  fields), brace/newline rules, CRLF handling consistent with `.gitattributes`. Run `dotnet format` once to apply.
- [x] Test coverage with coverlet (`dotnet test tests/UrDeck.Core.Tests -p:CollectCoverage=true`); floor for UrDeck.Core is 65% line (measured 70.6%).
- [x] `AGENTS.md` (build/test/verify commands, conventions, the Windows/WPF gotchas above; `CLAUDE.md` can point to it).
- [x] `CONTRIBUTING.md`: move the widget-authoring section out of README; add performance budgets (item 4) and the
  review checklist.
- [x] GitHub Actions CI on `windows-latest`: build, test, `dotnet format --verify-no-changes`, `openspec validate --all --strict`.
- [x] Product name decided: **UrDeck**. License decided: see "Naming, licensing and hosting" below.
- [ ] Placeholder app icon (task 9.1/9.2 of `urdeck-framework`); final logo/branding deferred.

### Git workflow and GitHub maintenance

**Status:** applied on 2026-09-29 (ruleset "main protection", squash-only merge settings, PR template, Dependabot, `pr-title` check). Open: `CODEOWNERS` once there is a second maintainer.

All work happens on a short-lived branch and lands on `main` through a pull request. Nobody (including admins and
agents) pushes to `main` directly. Configure on GitHub (repo settings + a ruleset on `main`), and document in
`CONTRIBUTING.md`:

- Ruleset on `main`: require a pull request (no direct pushes, no force pushes, no deletion), require the CI status
  checks (`build`, `pr-title`) to pass with branches up to date, require linear history, require conversations
  resolved. No bypass for admins. Zero approving reviews are required while there is a single maintainer; raise
  `required_approving_review_count` when that changes.
- Merge methods: squash only (disable merge commits and rebase merges), which also guarantees linear history.
- Squash commit defaults: title = PR title, body = PR description (`squash_merge_commit_title: PR_TITLE`,
  `squash_merge_commit_message: PR_BODY`), so the PR text is the commit text.
- Delete head branches automatically after merge; enable "always suggest updating pull request branches".
- Pull request template (summary, linked OpenSpec change, verification done, perf impact) and a conventional-commit
  PR title check in CI.
- Optional: Dependabot for NuGet and GitHub Actions; `CODEOWNERS` once there is more than one maintainer.

### Naming, licensing and hosting (decided 2026-09-29)

- [x] **Name:** UrDeck (short for "your deck"). Renamed from Widgy: namespaces, assemblies, exe, config (`urdeck-config.json`),
  log (`urdeck.log`), widget type ids (`urdeck.widgets.clock`), analyzer ids (`URDECK001-005`), env vars.
- [x] **GitHub:** the repo lives in the `UrDeck` org as `UrDeck/urdeck` (moved 2026-09-29; the old URL redirects).
- [ ] Reserve the `UrDeck.` NuGet prefix; register `urdeck.app` / `urdeck.dev` (owner task).
- [x] **Licenses** (done: root `LICENSE`, `PLUGIN-EXCEPTION.md`, per-directory `LICENSE` for the MIT analyzer, SPDX headers
  enforced by `.editorconfig`, `PackageLicenseExpression` in the build props, README license map). Interim state:
  `UrDeck.Core` is GPL until the SDK/Engine split below, so only the analyzer is MIT today. The plan:
  - Host, engine and official widgets: **GPL-3.0-or-later** with a GPLv3 section 7 **plugin exception**: widgets that use only
    the public SDK API may be under any license.
  - SDK and analyzer (`UrDeck.Sdk`): **MIT**. Templates and example widgets: MIT (so copying them does not make a
    community widget GPL). Fonts, icons, themes and logo: licensed separately.
  - Community widgets: the author chooses. Contributions are inbound=outbound (no DCO/CLA before 1.0 or the first
    outside contribution; revisit if dual licensing is ever wanted).
- [ ] **SDK/Engine split** (before the theme engine, item 5): the license boundary must be an assembly boundary. Split
  `UrDeck.Core` into `UrDeck.Sdk` (MIT: attributes, `Widget<T>`, render context, theme types, analyzer bundled) and
  `UrDeck.Engine` (GPL: plugin loader, config store, grid layout, `PageRenderer`). Widgets reference only the SDK.
  The SDK is not widget-only: data providers, display backends and theme packs will use it too.
- **Repo strategy:** monorepo through 1.0 (atomic SDK/engine/host/widget changes while the API churns). Later, split along
  the existing seams: `urdeck/widget-template` (MIT template repo, worth doing soon) and a git-based marketplace
  registry repo (see item 13). Split the SDK out only once it is stable.

## 2. Roadmap document

This file. Keep "Current state" and item status up to date at the end of each session.

## 3. Finish refresh strategies, then archive `urdeck-framework`

**Why:** the SDK promises three refresh strategies; only `[RefreshOnTick]` fully works.
**Model:** Sonnet (Opus if the event-bus design gets contentious).

- `[RefreshOnEvent("name")]` (task 10.1): an in-process event bus in UrDeck.Core. Publishers (future data providers
  like a sensor service) publish by name; the host marshals to the UI thread and invalidates subscribed widgets.
  Subscriptions must be dropped on plugin reload. Today such widgets render once.
- `[RefreshAdaptive(minMs, maxMs)]` (task 10.2): scale the interval between min and max based on system load
  (e.g. process/system CPU) or on-battery; today it runs at `minMs`.
- Skip redundant redraws: let a widget report "nothing changed" (e.g. `UpdateAsync` returning a bool, or a
  `bool NeedsRender(DateTime now)`); the Clock should repaint once a minute, not every second.
- Placeholder icon (see item 1), then run the OpenSpec archive workflow for `urdeck-framework`.

## 4. Performance budgets and measurement

**Why:** give users and widget authors clear, comparable costs (the per-component strategy).
**Model:** Opus for the measurement design, Sonnet to implement.

- Budgets: host runtime baseline 60-70 MB private; editor is secondary; per-widget budgets with tiers
  (e.g. gold < 5 MB, silver < 10 MB, bronze above), expressed **per grid size** because the render surface
  (width x height x 4 bytes) dominates per-widget memory.
- Measurement: a benchmark mode (e.g. `UrDeck.Host.exe --bench <widgetId> --size 4x2`) that runs the host with only
  that widget and reports the delta from an empty page (private bytes, working set), plus per-widget render time,
  update time and allocations per frame (`GC.GetAllocatedBytesForCurrentThread` around `Render`/`UpdateAsync`).
  Per-widget memory can't be isolated precisely inside a shared process; isolation runs are the fair measure.
- Optional in-app diagnostics overlay showing per-widget render time and CPU.
- Publish tiers in CONTRIBUTING.md; later show them in the widget picker.
- Open decision: if the host baseline is unacceptable, evaluate a plain Win32 window + Skia host (est. 20-30 MB).

## 5. Theme engine (`openspec/changes/theme-engine`)

Shared colors, typography, panel/card styling, spacing, corner radius, exposed via `WidgetRenderContext`;
user-selectable themes. The Clock's rounded panel card becomes a theme primitive. Do this before building more
widgets so they don't each invent styling. **Model:** Sonnet (Opus for the API design review).

## 6. Display targeting (`openspec/changes/display-targeting`)

Pick the target monitor from a UI list with friendly names (EDID), persist a stable monitor identity (not
`DISPLAYn`), never expose resolution/scaling to the user, and re-render at the correct scale after sleep/resume,
hot-plug, mixed-DPI setups and arbitrary monitor wake order. Needs real sleep/wake testing on the Y70.
**Model:** Opus (hard to debug).

## 7. Default widget set

Clock (done), weather, CPU/memory/GPU usage, media/now-playing. Notes: sensor data likely via LibreHardwareMonitor
(some sensors need admin; run as a separate provider publishing on the event bus); weather via a keyless API such as
Open-Meteo. Each widget must meet its performance budget (item 4). **Model:** Sonnet.

## 8. Pages and touch

Multiple pages with swipe navigation and a page indicator; tap/touch interaction routed to widgets (WPF touch and
manipulation events; add an input API to the SDK). **Model:** Sonnet, Opus for the input API design.

## 9. Backgrounds

Static image, video, and generative visualizations per page (the `background` config field exists but is unused).
Video is expensive: gate it behind the performance budget and measure. **Model:** Sonnet.

## 10. Dock / quick launch

Launcher bar for apps and URLs (the `dock` config field exists but is unused). **Model:** Sonnet.

## 11. WYSIWYG editor

Drag/drop placement on the grid, resize within supported sizes, property editing from each widget's config type,
widget palette with performance tiers. Likely a separate window on the primary monitor editing the live page.
**Model:** Opus for design, Sonnet for implementation.

## 12. Packaging and distribution

Installer, start with Windows, tray icon, auto-update, logo and branding, winget manifest and GitHub release automation, and a third-party notices file
(SkiaSharp is MIT and must be attributed in binary releases). **Model:** Sonnet.

## 13. SDK distribution and other displays

- Publish `UrDeck.Sdk` as a NuGet package with the analyzer bundled; a `dotnet new` widget template.
- Marketplace: start as a git-based registry repo (winget-pkgs / Scoop bucket model): widgets are added by PR with a
  manifest (id, version, SDK version range, SPDX license, requested capabilities, file hash); CI validates it and the
  host reads a static index. Widgets are unsandboxed .NET DLLs, so plan signing/review and capability declarations early.
- Cross-brand support: some case screens are Windows monitors (like the Y70), others are USB LCDs driven by vendor
  protocols. `PageRenderer` already renders a page to a bitmap, which is the basis for "display backends" that push
  frames to non-monitor devices.
