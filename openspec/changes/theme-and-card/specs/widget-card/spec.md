## Purpose

Defines the card drawn behind every widget: who draws it, what shape it has and how widget content relates to it, so
that every widget on a page shares one card shape that only the theme controls.

## ADDED Requirements

### Requirement: Host-Drawn Card
The host MUST draw a card for every placed widget before the widget renders, using the theme's card fill, card border,
border width and corner radius. The card covers the widget's whole surface: the surface and the card are the same
rectangle. Widgets MUST NOT need to draw their own background or card.

#### Scenario: Widget draws only content
- **WHEN** a widget's render draws nothing
- **THEN** its place on the page shows an empty card in the theme's card fill

#### Scenario: Theme changes the card
- **WHEN** the same widget renders under two themes with different card fills and corner radii
- **THEN** its card differs accordingly without any change to the widget

### Requirement: Card Shape Is Page-Wide
Every card on a page MUST use the same corner radius and border width, whatever the widget's grid size. The corner
radius is limited to half of the card's smaller side. A corner radius of 0 gives square cards. Neither a widget nor a
per-widget setting can change or remove the card.

#### Scenario: Different widget sizes
- **WHEN** a 4x2 widget and a 1x1 widget are on the same page
- **THEN** both cards have the same corner radius and border width in pixels

#### Scenario: Flat tiles
- **WHEN** the theme's corner radius is 0
- **THEN** every card on the page is drawn with square corners

### Requirement: Content Is Clipped To The Card
Widget content MUST be clipped to the card's shape, including its rounded corners, so a widget that fills its whole
surface still shows the card's shape. A widget MAY draw over the card fill up to the card's edge.

#### Scenario: Edge-to-edge content
- **WHEN** a widget fills its entire surface with an opaque colour under a theme with rounded corners
- **THEN** the result has the card's rounded corners and nothing is drawn outside them

### Requirement: Themed Error State
When a widget's render fails, the host MUST show the failure inside that widget's card, in the card's shape, using the
theme's critical colour, with the widget name and the error message. Other widgets are unaffected and the failure is
logged.

#### Scenario: Widget render throws
- **WHEN** a widget throws during render
- **THEN** its card shows the widget name and message in the theme's critical colour, clipped to the card shape

### Requirement: Same Card Everywhere
The card MUST look the same in the live window and in a snapshot rendered without a screen.

#### Scenario: Snapshot matches the window
- **WHEN** a page is rendered with `--snapshot` at the live window's pixel size
- **THEN** each card's position, size, fill, border and corner radius match the live window
