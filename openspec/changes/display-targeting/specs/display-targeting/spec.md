# Display Targeting Specification

## ADDED Requirements

### Requirement: Monitor Listing and Identification
The application MUST let the user see and identify the connected displays:

- Each display is listed with a friendly name (model name where the system provides one, otherwise a generic name), its resolution, orientation, primary flag and relative position
- The user can trigger an identify action that briefly shows an on-screen marker on every connected display so the list entry can be matched to the physical screen
- The user is never required to know a display's resolution, device name or Windows scaling factor to select it

#### Scenario: Two displays connected
- **WHEN** the user opens the monitor picker with a primary display and a vertical panel connected
- **THEN** both are listed with friendly names and resolutions, and the identify action shows a marker on each

#### Scenario: Display without model information
- **WHEN** a display reports no friendly name
- **THEN** it is listed with a generic name plus its resolution and position

### Requirement: Selecting the Target Display
The user MUST be able to choose the target display from the list, and the choice MUST take effect without restarting:

- Selecting an entry moves the dashboard to that display immediately
- The dashboard covers the selected display's full area and lays out at the correct scale for that display's resolution and scaling
- The choice is persisted

#### Scenario: User picks the vertical panel
- **WHEN** the user selects the 1100×3840 panel in the picker
- **THEN** the dashboard covers that panel exactly and is laid out for its size

### Requirement: Stable Persisted Identity
The selected display MUST be persisted using an identity that survives re-plugging, reboots and re-enumeration:

- The identity is based on stable display attributes (device path or manufacturer/product/serial when available), not on enumeration order or `DISPLAYn` names
- Configurations written by earlier versions (`monitorName`, `monitor`) remain valid and are migrated to the stable identity the first time the selection is saved
- If several displays match the persisted identity (identical monitors without serials), the one whose position and size match the previous placement is preferred

#### Scenario: Cables re-plugged
- **WHEN** display enumeration order changes between runs
- **THEN** the dashboard still appears on the previously selected physical display

#### Scenario: Legacy configuration
- **WHEN** the config contains only `monitorName: "tallest"`
- **THEN** it continues to work, and selecting a display in the picker replaces it with the stable identity

### Requirement: Correct Placement After Display Changes
The application MUST converge to a correct placement after display changes:

- After resume from sleep, display hot-plug or unplug, resolution change and DPI change, the window covers exactly the target display's bounds and content is laid out for the display's current size and scale
- Monitors that return in arbitrary order or settle their scaling late MUST NOT leave the dashboard on the wrong screen, mis-sized, or blurry; placement is verified against the actual display state rather than assumed after a fixed delay
- Mixed-DPI setups (displays with different scaling) are supported: content is rendered at the target display's physical resolution
- Repeated display events are coalesced so the layout is not rebuilt redundantly

#### Scenario: Resume from sleep
- **WHEN** the computer wakes and the displays reappear in a different order
- **THEN** within a few seconds the dashboard covers the correct display at the correct scale without user action

#### Scenario: Mixed DPI
- **WHEN** the primary display uses 150% scaling and the target panel uses 100%
- **THEN** the dashboard renders on the panel at native resolution with no scaling blur

### Requirement: Absent Target Display
The application MUST behave predictably when the selected display is not connected:

- By default the dashboard does not appear on any other display; the application keeps running, logs the reason, and waits
- When the selected display becomes available, the dashboard is placed on it automatically
- An option allows falling back to the primary display instead

#### Scenario: Target unplugged at startup
- **WHEN** the application starts and the selected display is not connected
- **THEN** no window covers another display, and the reason is logged

#### Scenario: Target reconnected
- **WHEN** the selected display is connected while the application is running
- **THEN** the dashboard appears on it without restarting

#### Scenario: Fallback option enabled
- **WHEN** the target is absent and the primary-fallback option is enabled
- **THEN** the dashboard covers the primary display until the target returns
