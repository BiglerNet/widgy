# Handoff: explore the theme engine and shared components (2026-10-03)

Context for a fresh session (Opus recommended) that will run `/opsx:explore` and then `/opsx:propose` for UrDeck's
theme and shared rendering components. Read `AGENTS.md`, `README.md` and `docs/ROADMAP.md` first; this note only adds what
those do not say.

## Start here

1. `/model opus`, then ask: "Read `docs/handoff/2026-10-03-theme-and-components.md`, then run `/opsx:explore` on the
   theme and components design with me."
2. Explore with the owner (conversation, no code). Settle the open questions below.
3. `/opsx:propose` a new change (suggested name: `theme-and-components`). It **replaces** the draft
   `openspec/changes/theme-engine`: when the new change is created, move the old draft to `docs/design/` as reference
   (as was done for `docs/design/sdk-engine-split-full-*.md`) so `openspec list` shows only live work.
4. Implement with `/opsx:apply` on a cheaper model (Sonnet), then `/opsx:archive`. One PR per change, branch off `main`.

## Where things stand

- Repo `UrDeck/urdeck` (public, `main` protected: PRs only, squash merge, PR title is a conventional commit, CI checks
  `build` and `pr-title`). Rules and commands are in `AGENTS.md`.
- The `urdeck-framework` change is finished and archived; current capability specs are in `openspec/specs/`
  (grid-layout, host-shell, widget-clock, widget-sdk).
- Layout: `sdk/` (MIT: `UrDeck.Sdk`, `UrDeck.Analyzer`), `src/` (GPL: `UrDeck.Engine`, `UrDeck.Host`), `widgets/`,
  `tests/`. Widgets reference only `UrDeck.Sdk`; that is the license boundary.
- Active but stale OpenSpec drafts (written without the explore/propose workflow, before the rename and SDK/Engine
  split): `theme-engine` (0/15 tasks) and `display-targeting` (0/19). `data-providers` is not written yet.
- Open owner tasks (not blocking): reserve the `UrDeck.` NuGet prefix, register `urdeck.app` / `urdeck.dev`.

## What exists today (theme-related facts, verified)

- `sdk/UrDeck.Sdk/Rendering/ThemeColors.cs`: a 5-color record struct (`TextColor`, `BackgroundColor`, `AccentColor`,
  `PanelBackgroundColor`, `PanelHeaderColor`) with one value, `DefaultDark`.
- `WidgetRenderContext` (SDK) carries `Theme` (a `ThemeColors`), the canvas, time, pixel size, config and a token.
- The host ignores the `theme` config key (`UrDeckConfig.Theme`, default `"default-dark"`) and uses
  `ThemeColors.DefaultDark` directly (`MainWindow.cs`, `App.xaml.cs`).
- The Clock (`widgets/UrDeck.Widgets.Clock/ClockWidget.cs`) decides its own look: a rounded "panel card" (inset and
  radius proportional to the smaller side), Segoe UI Semibold/Light typefaces, an alpha-dimmed date, fit-to-width text.
  The owner liked the card; it should become a shared primitive.
- `src/UrDeck.Host/Resources/DefaultTheme.xaml` is WPF window chrome and is out of scope.
- `NeedsRender(now)` exists on `IWidget` (a widget can say nothing changed); the host repaints only when it returns true.

## What the old `theme-engine` draft proposed (salvage what fits)

Theme as JSON data (colors, typography roles, panel style, spacing, all in relative units so it stays resolution
independent); built-in themes embedded (dark, light, high-contrast) plus user files in a `themes/` folder with hot-reload;
selection through the existing `theme` config key with fallback to default plus a warning; a `Style` object on
`WidgetRenderContext` offering `DrawPanel`, `GetTextStyle(role, size)` and `FitText`, cached per theme; the Clock migrated
onto it with a `--snapshot` before/after comparison at 1100x3840. Non-goals: a theme editor UI, animated themes, WPF
chrome theming.

## The layering to validate (proposed in conversation, not yet confirmed by the owner)

The draft blends design tokens with drawing helpers in one `Style` object. The proposal is to split them:

| Layer | What | Where |
|---|---|---|
| Theme | Pure data (colors, type roles, spacing, radius, panel style, animation timings); user selectable; theme packs would be data-only (safe for a future marketplace) | Small model in `UrDeck.Sdk`; loading and selection in `UrDeck.Engine` |
| Components | Code that draws using the theme: panel, text block with fit-to-width, vector icons, gauge, sparkline/line and bar charts, progress ring | New MIT assembly under `sdk/` (working name `UrDeck.Sdk.Components`), separate from the core contract because it changes faster and is optional |
| Widgets | Compose components and decide what to show | Official and community widgets |

The loader already shares any assembly the host can supply, so a components assembly needs no loader change, but it needs
the same frozen-`AssemblyVersion` discipline as the SDK.

## Open questions for the exploration

1. Confirm the three layers, the assembly name, and what the minimal v1 component set is (suggest: panel, text with fit,
   one icon mechanism, sparkline, gauge; defer the rest).
2. The token set and typography roles. Relative units: relative to what (cell size, the smaller panel side)? The user must
   never see resolution or scaling, and grid cells are `width / 4` (275 px on the 1100x3840 panel).
3. Built-in themes for v1 (dark plus light enough?), user theme files, hot-reload, fallback rules.
4. Icons: how to draw them (vector path data via Skia needs no extra dependency) and which permissively licensed set to
   bundle (Lucide is ISC, Fluent is MIT; keep attribution).
5. Animation: it needs a "request frames for a while" API, which interacts with `NeedsRender`, idle-CPU goals and the
   future adaptive refresh. Suggest static components plus simple transitions in v1 and a frame-request API later.
6. Chart data: keep charts data-agnostic (they take values); history buffers live in the widget or come from the future
   data providers.
7. Per-widget overrides such as the Clock's `textColor`: keep in widget config or generalize?
8. Backward compatibility: additive changes only (the SDK `AssemblyVersion` is frozen at 0.1.0.0 and a test guards it).
   Keep the existing `ThemeColors` working through migration.

## Constraints and preferences

- **Keep it small.** The owner pushed back when the SDK/Engine design grew heavy ("this may have gotten very
  complicated"). Design the minimum v1, and record the rest in the roadmap's "Parked" lists instead of building it.
- Performance is priority one: idle CPU near zero, ~66 MB private baseline (60-70 MB budget), software WPF composition, so
  measure gradients and shadows at 1100x3840 before making them defaults.
- The SDK is MIT, so it must not depend on GPL code; widgets never reference `UrDeck.Engine` or `UrDeck.Host`.
- Warnings are errors, files need SPDX headers, package versions are central. Verify with build, tests,
  `dotnet format --verify-no-changes`, `openspec validate --all --strict` and `UrDeck.Host.exe --snapshot`.
- Delegate implementation to a cheaper model; keep Opus for design and review.

## Parallel track, not part of this exploration

`data-providers` (roadmap item 3): shared topics, pluggable providers as an SDK plugin kind, demand-driven polling,
adaptive refresh built on them. The direction is written down in the roadmap; the full design wants its own Opus review
and its own change after this one. Parked SDK hardening items are listed in the roadmap under "Parked".

## Pointers

`docs/ROADMAP.md` (items 3, 5, 7, 11; "Naming, licensing and hosting"; "Parked"), `openspec/changes/theme-engine/`
(draft), `openspec/specs/widget-clock/spec.md` (the Clock's visual contract), `docs/design/sdk-engine-split-full-design.md`
(reference only), `docs/perf/memory-investigation.md`.
