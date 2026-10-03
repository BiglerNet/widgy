## Why

Every visual decision currently lives inside the Clock widget (its card, inset, radius, fonts, text sizes), and the
host ignores the `theme` config key. Before more widgets are built, the look has to move out of widgets so that every
widget on a page shares one card shape, one gutter and one set of text styles, and so that a new look can be made by
changing values in a theme rather than code.

## What Changes

- **Theme as data.** A theme is a folder holding one settings file and optional font files. It defines colours (with
  transparency), card shape (corner radius, border, gap, padding), typography (font family and a weight per text role,
  three small-text sizes, the unit ratio) and stroke style. Sizes are stored as fractions of the grid cell; widgets only
  ever receive finished pixel values.
- **Built-in and user themes.** Two built-in themes ship with the product and cannot be broken by the user. User themes
  live in a `themes/` folder next to the executable, may be partial (missing values come from the default theme) and may
  bundle their own fonts. The existing `theme` config key selects the theme; an unknown or invalid theme falls back to
  the default with a warning. A theme switch applies on config reload without a restart.
- **The host draws the card.** The engine draws each widget's card (fill, border, rounded clip) from the theme before
  the widget renders. A widget's surface is exactly its card. Widgets cannot opt out; a theme with radius 0 gives flat
  tiles for the whole page. The render-error state is drawn as a themed card.
- **The gap moves into the grid layout.** Each grid cell is inset by half the theme's gap, so gutters are identical
  between cards of any size.
- **Two shared components:** a readout (value, unit, optional label) and a text line with shrink-to-fit. Values fit the
  slot the widget gives them and stay the same size as the number changes; labels, body and title text use page-wide
  sizes from the theme.
- **Clock migrated.** The Clock drops its own card, fonts and sizing and is built from the readout and text line.
- **BREAKING (SDK):** `ThemeColors` is removed. `WidgetRenderContext.Theme` becomes the new theme type, the context
  gains the content rectangle, and its size now means the card's size. The SDK assembly version is bumped deliberately.
- The old `theme-engine` draft is retired to `docs/design/theme-engine-draft-*.md` as reference.

Out of scope, each for a later change: animation and a frame-request API, GPU-backed rendering, gauge, icons and
glyphs, image tiles, charts, shadows, gradients, blur, backgrounds, a theme editor, per-widget theme overrides, and
watching theme files for edits.

## Capabilities

### New Capabilities

- `theme`: what a theme defines, the theme folder format, built-in and user themes, selection, fallback, and how
  theme values reach widgets.
- `widget-card`: the host-drawn card behind every widget, its shape and clipping, and the themed error state.
- `components`: the shared drawing components available to widgets (readout and text line) and their sizing rules.

### Modified Capabilities

- `grid-layout`: grid-to-pixel conversion produces a card rectangle inset by half the theme's gap.
- `widget-sdk`: the render context carries the new theme and the content rectangle; `ThemeColors` is gone.
- `host-shell`: each widget element is placed at its card rectangle; the window background, error tile and snapshot
  follow the selected theme; `--snapshot` accepts a theme name.
- `widget-clock`: the Clock's appearance comes from the theme, the host-drawn card and the shared components.

## Impact

- `sdk/UrDeck.Sdk`: new theme type and components, changed `WidgetRenderContext`, `ThemeColors` deleted, assembly
  version bump (and the test that guards it).
- `src/UrDeck.Engine`: theme loading, merging and resolving; card painting in the shared widget paint path; gap in
  `GridLayoutManager`; `PageRenderer` signature.
- `src/UrDeck.Host`: theme selection on startup and config reload, window background, element placement, `--theme`.
- `widgets/UrDeck.Widgets.Clock`: rewritten on the components.
- New bundled asset: one openly licensed variable font, with its licence text and an attribution entry.
- Docs: `README.md`, `CONTRIBUTING.md` (widget example), `AGENTS.md` (layout line), `docs/ROADMAP.md`.
- Performance: one extra rounded fill and an antialiased clip per widget paint; to be measured at 1100x550.
