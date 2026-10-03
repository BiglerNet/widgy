# Theme Engine — Shared Style System for Widgets

## Why

Visual decisions are currently made inside each widget. The Clock picks its own fonts, draws its own rounded "panel card" (inset, corner radius, `PanelBackgroundColor`) and dims its date to ~80% alpha. The only shared piece is `ThemeColors`, a five-color palette with a single built-in value (`DefaultDark`); the `theme` config field is persisted but never read, and the WPF `DefaultTheme.xaml` is unrelated to what widgets draw.

As more widgets arrive (gauges, charts, weather, media), every author would reinvent cards, typography and spacing, and the dashboard would look inconsistent. Looking exceptional is UrDeck's priority #2, and that needs one place to define the look.

## What Changes

- Introduce a **theme** as a first-class, named, data-driven definition: color palette, typography roles (time/display, title, body, caption; family, weight, relative size), panel/card style (fill, border, corner radius, inset, optional gradient/shadow), spacing scale, and accent/state colors.
- Widgets receive the active theme through `WidgetRenderContext` and draw using **theme primitives** (e.g. "draw a panel", "get the text style for role X", "measure and fit text in a rect") instead of hard-coded values.
- The Clock's panel card becomes a **theme-provided primitive**; the Clock keeps only content layout (time/date positioning and fit-to-width).
- Themes are **user-selectable**: built-in themes ship with the host (at least default dark plus a light and a high-contrast variant), selected by the existing `theme` config key and hot-reloaded like other config; users can add theme files.
- Unknown or invalid theme names fall back to the default theme with a logged warning.
- The current `ThemeColors` struct remains as the color part of the theme for source compatibility of existing widgets during migration.

## Capabilities

### New Capabilities
- `theme-engine`: Theme definition, built-in and user themes, selection and hot-reload, and the styling primitives widgets consume through the render context.

### Modified Capabilities
- `widget-clock` (defined in the unarchived `urdeck-framework` change): its "Clock Visual Appearance" requirement is satisfied via theme primitives rather than widget-local drawing. To be reconciled when both changes are archived.
- `widget-sdk` (same): `WidgetRenderContext` gains the theme style API.

## Impact

- **Code**: `UrDeck.Sdk` (theme types, `WidgetRenderContext`) and `UrDeck.Engine` (theme loader, primitives), `UrDeck.Host` (theme selection/hot-reload, passing the theme to views), `UrDeck.Widgets.Clock` (drop local card/fonts).
- **Config**: `theme` config key becomes functional; optional `themes/` folder for user theme files.
- **Compatibility**: Third-party widgets built against the current `WidgetRenderContext` continue to work; new members are additive.
- **Performance**: Typefaces and paints are cached per theme; theme changes trigger one repaint of all widgets, and idle CPU must remain near zero.
- **Non-goals**: a theme editor UI, per-widget theme overrides beyond what exists today (`textColor`), animated themes, theming the WPF window chrome.
