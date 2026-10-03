# Grid Layout Specification

## Purpose

Defines the resolution-agnostic 4-column grid UrDeck uses to place and size widgets on any monitor.

## Requirements

### Requirement: 4-Column Grid System
The layout engine MUST provide a deterministic 4-column grid system where:

- All widgets are placed on a grid with exactly 4 columns
- Widgets can span 1-4 columns in width (integer values only)
- Widgets can span 1-N rows in height (integer values only)
- Each column has equal width: `ColumnWidth = monitorWidth / 4`
- Each row has equal height: `RowHeight = ColumnWidth` (square grid cells)
- Widget positions are in grid units, not pixels

#### Scenario: Grid is square-cell based
- **WHEN** a monitor is 1100 pixels wide
- **THEN** each grid column is 275 pixels wide and each row is 275 pixels tall

#### Scenario: Widget spans multiple columns
- **WHEN** a widget is configured with `width: 4` on an 1100px monitor
- **THEN** the widget occupies the full monitor width (1100 pixels)

#### Scenario: Widget position stored in grid units
- **WHEN** a widget is placed at `col: 0, row: 0, width: 2, height: 1`
- **THEN** the widget's pixel position at 1100px monitor is `(0, 0)` with size `(550, 275)`

### Requirement: Grid-to-Pixel Conversion
The layout engine MUST convert grid units to pixel coordinates at render time based on the active monitor's current dimensions:

- `PixelX = Col * ColumnWidth`
- `PixelY = Row * RowHeight`
- `PixelWidth = Width * ColumnWidth`
- `PixelHeight = Height * RowHeight`

#### Scenario: Conversion for 3840×2160 monitor
- **WHEN** a monitor is 3840×2160 and a widget has `col: 1, row: 2, width: 2, height: 3`
- **THEN** `ColumnWidth = RowHeight = 960`, so the widget's position is `(960, 1920)` with size `(1920, 2880)`

#### Scenario: Conversion for 7680×2160 monitor (triple 4K)
- **WHEN** a monitor is 7680×2160 and a widget has `col: 0, row: 0, width: 4, height: 2`
- **THEN** `ColumnWidth = RowHeight = 1920`, so the widget's position is `(0, 0)` with size `(7680, 3840)`

### Requirement: Monitor-Aware Layout Manager
The layout engine MUST be resolution-agnostic and cheap to recompute when the target monitor changes:

- `GridLayoutManager` is constructed from (and `RenderWidgetLayout` recomputes for) a screen size; it holds no monitor-specific state
- The host detects monitor changes (hot-plug, resolution change, retargeting via config) and rebuilds the layout from the new window size, recalculating grid dimensions and widget pixel positions
- Layout is computed from the window size in device-independent pixels; per-widget surfaces render at the monitor's physical resolution
- Does NOT persist monitor-specific pixel positions (only grid-unit positions are persisted)

Note: `GridLayoutManager` declares a `MonitorChanged` event that is not raised; monitor-change handling lives in the host, which simply rebuilds the layout.

#### Scenario: Monitor resolution changes
- **WHEN** the monitor resolution changes from 1100×3840 to 1100×2160
- **THEN** grid column width remains 275 pixels but the number of rows decreases

#### Scenario: Target monitor changes
- **WHEN** the configured target monitor changes to one with a different resolution (e.g. 1080×1920)
- **THEN** the host recalculates column width and every widget's position and size for the new monitor

### Requirement: Layout Validation
The layout engine MUST validate widget placement during layout computation. Invalid values are clamped (never rejected) and a warning is logged:

- A widget's `Width` must be between 1 and 4 (clamped first)
- A widget's `Col` must be ≥ 0 and `Col + Width` ≤ 4 (clamped to `4 - Width` after width is clamped)
- A widget's `Row` must be ≥ 0
- A widget's `Height` must be ≥ 1

#### Scenario: Widget would overflow horizontal bound
- **WHEN** a widget is placed at `col: 3, width: 2`
- **THEN** the layout engine logs a warning and clamps the position to `col: 2, width: 2`

#### Scenario: Negative row
- **WHEN** a widget is placed at `row: -1`
- **THEN** the layout engine logs a warning and clamps to `row: 0`
