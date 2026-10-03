# Theme Engine — Design

## Context

`ThemeColors` (5 colors, one `DefaultDark` value) is passed to widgets in `WidgetRenderContext`. Everything else visual lives in widgets: the Clock computes an inset, a radius, fonts and alpha itself. The host ignores the `theme` config key and uses `ThemeColors.DefaultDark` directly.

## Decisions

1. **Theme is data.** A `Theme` model (colors, typography roles, panel style, spacing) loaded from JSON. Built-in themes are embedded in `UrDeck.Engine`; user themes are `*.json` files in `themes/` next to the executable. All dimensions are relative (fractions of a reference unit or of the panel's smaller side) so themes are resolution-independent, consistent with the grid.
2. **Primitives on the context.** `WidgetRenderContext` gains a `Style` member (additive) exposing `DrawPanel(rect)`, `GetTextStyle(role, referenceSize)` and `FitText(...)`. It lives in `UrDeck.Sdk` on top of SkiaSharp so plugins need no new dependency. `Theme` (the `ThemeColors`) stays for compatibility.
3. **Caching.** `Style` caches `SKTypeface`s and paints per theme instance; the host creates one `Style` per theme change and passes it to every `WidgetView`.
4. **Selection.** The host resolves `config.Theme` on startup and on config reload; on change it swaps the `Style` and invalidates all views once. Unknown name → default + warning.
5. **Clock migration.** Replace the private `DrawPanel`, `TimeTypeface`/`DateTypeface` and the `0xCC` alpha with `Style.DrawPanel` and the display/caption roles. Behavior at 4×2 on the 1100×3840 panel must be visually unchanged under the default theme (verify with `--snapshot` before/after).

## Risks / Open Questions

- **Font availability**: themes reference families such as Segoe UI; fallback chain needed if missing (default typeface).
- **Plugin ABI**: adding members to `WidgetRenderContext` is source-compatible; plugins load against the shared `UrDeck.Sdk` so binary compatibility holds because the host and plugins share one Core version.
- **Gradients/shadows** may cost fill-rate at 1100×3840 in software rasterization; measure before making them defaults.
- Should widget-specific color overrides (`textColor`) stay in widget config or become a general per-widget style override?
