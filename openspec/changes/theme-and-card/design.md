## Context

See `proposal.md` for motivation. The facts that shape the approach:

- `WidgetPainter.RenderSafely` (engine) is the single paint path shared by the live `WidgetView` and the snapshot
  `PageRenderer`. It already draws one host-owned state, the error tile.
- Each widget has its own `SKElement`; layout is computed in DIPs and each element rasterizes at physical pixels.
- `GridLayoutManager` produces cell rectangles with no gap; the Clock creates the gutter by insetting its own drawing,
  which makes gutters and radii depend on widget size.
- `PluginLoadContext` shares any assembly the default context can supply, so anything in `UrDeck.Sdk` is shared with
  plugins without loader changes.
- There is exactly one widget (the Clock), it lives in this repo, and the SDK is unpublished. A breaking SDK change costs
  nothing now and will never be cheaper.
- SkiaSharp is 4.153.0, which supports variable font axes.
- Priorities: idle CPU near zero, a 60-70 MB private baseline, and a small design (the owner pushed back on an earlier
  design that grew heavy).

Decisions already taken with the owner in the exploration: the host draws the card and widgets cannot opt out; the theme
owns card shape page-wide; labels are widget content with a per-widget config override, not host-drawn title bars; the
knob list in the `theme` spec; the sizing model in the `components` spec; continuous animation is a design target for a
later change.

## Goals / Non-Goals

**Goals:**

- A new look can be produced by editing theme values only.
- Widgets contain no styling decisions and no resolution or scaling arithmetic.
- One code path draws cards for the live window, snapshots and future display backends.
- Nothing here blocks the later animation or GPU work: components draw on an `SKCanvas` and do not care what backs it.

**Non-Goals:**

- Choosing the final default look. This change ships two working themes; tuning their values is done by the owner
  looking at the panel, and needs no further code.
- Animation, frame requests, GPU-backed elements, gauge, glyphs, icons, image tiles, charts.
- Shadows, gradients, blur, background images or video.
- A file watcher for theme folders, a theme editor, per-widget theme overrides.
- A placeholder card for an unregistered widget type (the cell stays empty, as today).
- Loader hardening against plugins built for the earlier SDK (parked in the roadmap).

## Decisions

### 1. Two theme types: a definition in the engine, a resolved theme in the SDK

`ThemeDefinition` (engine) mirrors the settings file: colours as strings, sizes as fractions of the cell, every member
nullable so a partial user theme can be merged over the default. `Theme` (SDK) is what widgets see through
`context.Theme`: `SKColor` values, sizes in pixels for one surface, and a typeface per text role. The engine resolves a
definition into a `Theme` for a given physical cell size.

*Why:* it keeps ratios, merging, parsing and fallback out of the MIT contract and guarantees a widget cannot do unit
arithmetic. *Alternative considered:* one type carrying ratios plus a cell size on the context. Rejected because every
widget would multiply, and would start to care about resolution.

`Theme` is a sealed class with get-only members and an internal-to-engine construction path (a public constructor or
init-only properties; tests and the engine build it). It owns its `SKTypeface`s and is `IDisposable`; the host disposes
the previous one after a rebuild.

### 2. Replace `ThemeColors` outright and bump the SDK assembly version

`ThemeColors` is deleted, `WidgetRenderContext.Theme` changes type, `ContentRect` is added, and the constructor changes.
`AssemblyVersion` goes from 0.1.0.0 to 0.2.0.0 and `SdkContractTests` is updated to the new frozen value.

*Why:* additive-only would leave the real theme under a second name forever to protect consumers that do not exist.
*Alternative considered:* keep `ThemeColors` as a projection. Rejected as permanent clutter in a pre-release contract.

### 3. Components live in `UrDeck.Sdk`, namespace `UrDeck.Sdk.Components`

Two static components (`Readout`, `TextLine`) do not justify a new assembly, a second frozen version, a host reference
and a packaging story. *Alternative considered:* a separate `UrDeck.Sdk.Components` assembly, as the handoff suggested.
Deferred: the property that matters is that components depend only on the canvas, a rectangle, the `Theme` and the
values passed in. While that holds, they can move to their own assembly later with type forwarding.

### 4. The gap is a layout input; the surface is the card

`GridLayoutManager` takes the gap as a fraction of the column width and returns, per widget, the card rectangle (cell
inset by half the gap on each side). The host sizes and positions each `SKElement` at the card rectangle, so
`PixelSize` is the card and the widget's origin is the card's corner. The outer page margin is therefore half the gap.

*Why:* no translate, no ambiguity about what `PixelSize` means, and no transparent margin pixels in each surface.
*Alternative considered:* keep the element at the cell and translate the canvas. Rejected: it wastes surface memory and
needs a second "which size" concept. *Alternative for the margin:* inset the whole grid so the outer margin equals the
gap. Not done now; it is a one-line layout change if the panel shows it looks better.

Layout stays in DIPs (the gap fraction is unitless). The theme is resolved against the physical cell size
(`physicalWidth / 4`), which is the unit the widget's surface uses. The host's "did the layout change" check must
include the DPI scale and the active theme, not only the DIP size.

### 5. Card painting sits in `WidgetPainter.RenderSafely`

Order per paint: draw the card (rounded fill, then border), save, clip to the rounded rectangle with antialiasing, call
the widget, restore. On an exception: restore to the clip, draw the error content (widget name and message in the
theme's critical colour, on the card fill) and log. `WidgetView` no longer clears to anything but transparent; the
corners outside the rounded rectangle stay transparent over the window background. `PageRenderer` translates to the
card rectangle and calls the same function.

*Alternative considered:* a WPF `Border` behind each element. Rejected: snapshots and future display backends would
need a second implementation that could drift.

The corner radius is clamped to half the card's smaller side. Paint objects are created per paint; with render skipping
that is negligible and avoids a cache that would have to follow theme changes.

### 6. Theme storage, merging and fallback

- A theme is `themes/<name>/theme.json` plus optional font files in the same folder. Built-in themes use the same layout
  but are embedded resources in `UrDeck.Engine` (settings and font), so they cannot be deleted or edited.
- Load order for a name: user folder first, then built-in. A user theme is merged over the built-in `default-dark`
  definition value by value, so a five-line theme is valid.
- Failure handling is per value where possible (bad colour → default value plus warning) and per theme otherwise
  (unreadable JSON → theme unavailable plus warning). The last resort is always the embedded `default-dark`, so the host
  can never fail to start because of a theme.
- Colours are `#RRGGBB` or `#AARRGGBB`. JSON uses the existing `UrDeckJson` options (camelCase).
- Themes are re-read on every config reload; there is no watcher on theme folders. Editing a theme and then saving the
  config applies it.
- A font path is resolved inside the theme folder and rejected if the resolved full path leaves it.

### 7. Fonts

The theme's font is either an installed family name or a bundled file. Each role's weight is applied through the
variable-font weight axis when the font has one, otherwise by asking for the nearest installed weight of the family.
Typefaces are created once when the theme is resolved.

The built-in themes bundle one variable font under the SIL Open Font License 1.1. Proposed: **Inter** (variable, OFL
1.1). It is a starting point, not the final look; the owner can swap it by replacing the file and one line of settings.
The font's licence text ships next to it, and the README licence map gains an entry. OFL permits bundling with both the
GPL engine and any later MIT packaging, provided the licence travels with the font and the font is not sold alone.

The implementer must confirm the variable-font API against the SkiaSharp 4.153 documentation (through Context7) before
writing that code, and verify the licence file in the font's upstream release.

### 8. Component behaviour

- **Readout.** Inputs: rectangle, value text, optional unit, optional label, optional widest-value sample, unit
  placement (raised or baseline), alignment, optional value colour, optional scale (0.1 to 1.0). The value size is the
  largest at which the sample (or the value, when no sample is given) plus the unit fits the rectangle's width, and the
  value's cap height plus the label line fits its height. The unit size is the value size times the theme's unit ratio.
- **Equal-width digits** are done by the component, not the font: each digit is drawn centered in the advance of the
  widest digit of the typeface; other characters use their own advance. This makes any theme font behave, with no text
  shaping dependency.
- **TextLine.** Inputs: rectangle, text, step (label, body, title), emphasis (primary or muted), alignment. Size is the
  theme's pixel size for the step, reduced only when the measured width exceeds the rectangle.
- Both return the rectangle they actually drew in, so a widget can stack them without measuring twice.
- Vertical placement uses cap height, as the Clock does today.

### 9. Clock migration

The Clock splits `ContentRect` into a time slot and, when the date is shown, a date slot sized from the theme's title
size. The time is a readout with sample `88:88`, bottom-aligned in its slot; `AM`/`PM` is the unit on the baseline; the
`fontSize` setting maps to the readout's scale. The date is a title text line in the muted colour. `textColor` overrides
the time's colour only. The private `DrawPanel`, `FitWidth`, `CapHeight` and both static typefaces are removed.

The Clock will not look identical to today: the card inset and radius now come from the cell, not from the widget's
smaller side. Verification is a before/after `--snapshot` at 1100x3840 that the owner looks at, not a pixel match.

### 10. Built-in theme values

`default-dark` starts from today's palette (background `#0f0f1a`, card `#1a1a2e`, text white, accent `#4a9eff`) with
cell-relative card values chosen so the 4x1, 2x1 and 1x1 Clock keeps roughly its current inset and radius.
`default-light` inverts the palette. Both are data; changing them later needs no code.

## Risks / Trade-offs

- **Antialiased rounded clip on every paint in software** → Measure paint time for a 1100x550 card with and without the
  clip and record it in `docs/perf/`. If the clip adds more than about 1 ms per paint, stop and raise it with the owner
  before continuing; the fallback is to clip only when the theme's radius is above zero and accept square clipping for
  flat themes.
- **Embedded font increases the engine assembly and memory** → Record private bytes before and after against the
  60-70 MB budget; one variable font file replaces several static weights.
- **Per-digit drawing loses kerning between digits** → Acceptable for numeric readouts; it is what fixed-width numerals
  do anyway.
- **Half-gap outer margin may look tight** → Decision 4 notes the one-line alternative.
- **Old plugins built against SDK 0.1.0.0 will fail at render, not at load** → None exist outside this repo; loader
  hardening is already parked in the roadmap.
- **A theme can make text unreadable (for example text colour equal to card fill)** → Not guarded; themes are the
  author's responsibility. The built-in themes are always selectable as a way back.
- **Theme edits need a config save to apply** → Accepted to avoid a second file watcher; documented in the README.

## Migration Plan

One pull request, branch off `main`. Order: SDK types and version bump, engine theme loading and resolving, layout gap,
card painting, host wiring, components, Clock, docs. No data migration: the `theme` config key already exists with the
value `default-dark`. Rollback is reverting the squash commit.

## Open Questions

- Final values for the two built-in themes, and whether Inter stays as the bundled font. Both are data changes the
  owner can make after seeing the panel.
- Whether the outer page margin should equal the full gap (Decision 4).
