# Theme Engine — Tasks

## 1. Theme model

- [ ] 1.1 Define the `Theme` model (colors incl. muted/state colors, typography roles, panel style, spacing) with relative units and JSON (de)serialization
- [ ] 1.2 Embed built-in themes: default dark (values matching today's `ThemeColors.DefaultDark` and the Clock's card), light, high-contrast
- [ ] 1.3 Theme loader for `themes/*.json` with fallback fill from the default theme and warning-and-skip on invalid files
- [ ] 1.4 Unit tests for parsing, defaults fill, precedence of user themes, invalid files

## 2. Style primitives

- [ ] 2.1 Add a `Style` object to `WidgetRenderContext` with `DrawPanel`, `GetTextStyle(role, size)` and `FitText`; keep `Theme` (`ThemeColors`) for compatibility
- [ ] 2.2 Cache typefaces and paints per theme instance
- [ ] 2.3 Unit tests with an off-screen surface (panel geometry scales with size; text fit never exceeds width)

## 3. Host integration

- [ ] 3.1 Resolve the `theme` config key at startup and on config reload; unknown name falls back to default with a warning
- [ ] 3.2 Pass the active `Style` to every `WidgetView`; repaint all views once on theme change; window background follows the theme
- [ ] 3.3 Watch the active theme file and hot-reload it
- [ ] 3.4 Verify idle CPU stays near zero with a theme active

## 4. Clock migration

- [ ] 4.1 Replace the Clock's local card, fonts and alpha with theme primitives and roles
- [ ] 4.2 Compare `--snapshot` output at 1100x3840 before/after under the default theme; differences must be intentional
- [ ] 4.3 Update `widget-clock` spec text if the visual contract changes

## 5. Docs

- [ ] 5.1 Document the theme file format and the style API for widget authors in the README
