# Theme Engine Specification

## ADDED Requirements

### Requirement: Theme Definition
A theme MUST be a named definition containing a color palette, typography roles, a panel/card style, and a spacing scale:

- Colors: text, background, accent, panel background, panel header (existing palette) plus muted text and state colors (positive, warning, critical)
- Typography roles: at least display, title, body and caption, each with font family, weight, and a size relative to a reference unit
- Panel style: fill color, optional border, corner radius, inset, and optional gradient or shadow, all expressed relative to the panel's size so they scale across resolutions
- Spacing scale: a small set of named spacing steps relative to a reference unit

#### Scenario: Theme provides every role
- **WHEN** a theme is loaded
- **THEN** every typography role and panel style value is defined, with omitted values filled from the default theme

### Requirement: Widgets Consume Theme Primitives
Widgets MUST be able to obtain styling from the theme through the render context rather than hard-coding it:

- Draw a panel/card in a given rectangle using the theme's panel style
- Obtain the text style (typeface, size, color) for a typography role at a given reference size
- Fit text into a width using a role's style

A widget that does not use theme primitives MUST continue to work unchanged.

#### Scenario: Clock uses the theme panel
- **WHEN** the Clock widget renders under two different themes with different panel radii
- **THEN** its card shape and fill differ accordingly without any change to the widget

#### Scenario: Legacy widget
- **WHEN** a widget written against the color-only theme API renders
- **THEN** it still receives the theme's colors and renders correctly

### Requirement: Built-in and User Themes
The host MUST ship built-in themes and support user-provided themes:

- Built-in themes include at least a default dark theme, a light theme, and a high-contrast theme
- Users can add theme files in a themes folder next to the executable; a user theme with the same name as a built-in one takes precedence
- An invalid user theme file is skipped with a logged warning and does not affect other themes

#### Scenario: User theme is discovered
- **WHEN** a valid theme file is placed in the themes folder
- **THEN** it can be selected by its name

#### Scenario: Broken theme file
- **WHEN** a theme file contains invalid content
- **THEN** it is ignored, a warning is logged, and the other themes remain available

### Requirement: Theme Selection and Hot-Reload
The active theme MUST be selected by the `theme` key in the configuration and applied without restarting:

- Changing `theme` in `widgy-config.json` (or editing the active theme file) repaints all widgets with the new theme
- An unknown theme name falls back to the default theme and logs a warning naming the missing theme
- The window background follows the theme's background color

#### Scenario: Switch theme at runtime
- **WHEN** the user changes `theme` from `default-dark` to `light` and saves the config
- **THEN** all widgets and the window background switch to the light theme within the config reload debounce period

#### Scenario: Unknown theme name
- **WHEN** the config names a theme that does not exist
- **THEN** the default theme is used and a warning is logged

### Requirement: Theming Does Not Cost Idle Resources
Theme handling MUST NOT introduce recurring work:

- Typefaces and paint resources are created once per theme, not per frame
- A theme change causes a single repaint of each widget

#### Scenario: Idle dashboard with themes
- **WHEN** the dashboard is idle with a theme active
- **THEN** no work occurs beyond each widget's own refresh timer
