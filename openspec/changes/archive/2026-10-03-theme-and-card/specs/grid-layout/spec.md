## ADDED Requirements

### Requirement: Card Rectangle
The layout engine MUST produce, for each widget, a card rectangle: the widget's cell rectangle inset on every side by
half of the theme's gap. The gap is a fraction of the column width, so it scales with the display. The card rectangle
is where the widget's surface is placed and is the size the widget draws at. A gap of 0 makes the card rectangle equal
to the cell rectangle.

#### Scenario: Gap between neighbouring cards
- **WHEN** the gap is 0.04 of the column width on an 1100 pixel wide monitor (11 pixels)
- **THEN** two horizontally adjacent 1x1 widgets have 11 pixels between their cards, and each card is 5.5 pixels from
  its cell's edges

#### Scenario: Gap does not depend on widget size
- **WHEN** a 4x2 widget sits directly above a 1x1 widget
- **THEN** the space between their cards equals the space between two adjacent 1x1 cards

#### Scenario: Zero gap
- **WHEN** the theme's gap is 0
- **THEN** each widget's card rectangle equals its cell rectangle

## MODIFIED Requirements

### Requirement: 4-Column Grid System
The layout engine MUST provide a deterministic 4-column grid system where:

- All widgets are placed on a grid with exactly 4 columns
- Widgets can span 1-4 columns in width (integer values only)
- Widgets can span 1-N rows in height (integer values only)
- Each column has equal width: `ColumnWidth = monitorWidth / 4`
- Each row has equal height: `RowHeight = ColumnWidth` (square grid cells)
- Widget positions are in grid units, not pixels
- A widget's cell rectangle is the block of grid cells it spans; its card rectangle (see Card Rectangle) lies inside it

#### Scenario: Grid is square-cell based
- **WHEN** a monitor is 1100 pixels wide
- **THEN** each grid column is 275 pixels wide and each row is 275 pixels tall

#### Scenario: Widget spans multiple columns
- **WHEN** a widget is configured with `width: 4` on an 1100px monitor
- **THEN** the widget's cell rectangle spans the full monitor width (1100 pixels)

#### Scenario: Widget position stored in grid units
- **WHEN** a widget is placed at `col: 0, row: 0, width: 2, height: 1`
- **THEN** the widget's cell rectangle at 1100px monitor is at `(0, 0)` with size `(550, 275)`

### Requirement: Grid-to-Pixel Conversion
The layout engine MUST convert grid units to the pixel coordinates of the widget's cell rectangle at render time based
on the active monitor's current dimensions:

- `PixelX = Col * ColumnWidth`
- `PixelY = Row * RowHeight`
- `PixelWidth = Width * ColumnWidth`
- `PixelHeight = Height * RowHeight`

#### Scenario: Conversion for 3840×2160 monitor
- **WHEN** a monitor is 3840×2160 and a widget has `col: 1, row: 2, width: 2, height: 3`
- **THEN** `ColumnWidth = RowHeight = 960`, so the widget's cell rectangle is at `(960, 1920)` with size `(1920, 2880)`

#### Scenario: Conversion for 7680×2160 monitor (triple 4K)
- **WHEN** a monitor is 7680×2160 and a widget has `col: 0, row: 0, width: 4, height: 2`
- **THEN** `ColumnWidth = RowHeight = 1920`, so the widget's cell rectangle is at `(0, 0)` with size `(7680, 3840)`
