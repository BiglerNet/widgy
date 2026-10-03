# Display Targeting — Pick a Monitor, Stay Correct

## Why

Today the target monitor is chosen by editing `urdeck-config.json`: `monitorName` is `primary`/`tallest`/`widest`/`largest` or a `DISPLAYn` GDI device name, with a 1-based `monitor` index as fallback. That works for the author's setup but is fragile and unfriendly:

- Users must know monitor names or indexes; nothing shows which physical screen is which.
- `DISPLAYn` names and enumeration order change when cables are re-plugged, GPUs re-enumerate, or monitors are added, so the saved target can silently point at the wrong screen after a reboot.
- After sleep/resume, hot-plug, or on mixed-DPI setups, monitors return in arbitrary order and settle their DPI late. The host currently mitigates this with retargeting at 0s, 1.5s and 5s, which is a heuristic, not a guarantee.
- When the configured monitor is absent (unplugged, powered off), the window falls back to the primary monitor, which can cover the user's main screen with a dashboard.

The goal: the user picks a screen from a list, never needs to know its resolution or Windows scaling, and UrDeck renders correctly on it at the right scale in every situation.

## What Changes

- **Monitor picker UI**: a list of connected displays showing friendly names (from EDID/display config, e.g. model name), resolution, orientation, primary flag and position, with a "Identify" preview that briefly flashes a number or overlay on each physical screen. Selecting one applies immediately and persists.
- **Stable monitor identity**: persist the selection using a stable identifier (display device path / EDID manufacturer+product+serial where available) instead of a `DISPLAYn` index, with fallbacks; keep reading legacy `monitorName`/`monitor` values and migrate them on first save.
- **Robust re-render**: after sleep/resume, hot-plug, resolution/DPI changes and mixed-DPI moves, converge to a window covering exactly the target monitor with correct DPI-based layout, driven by display-change events plus verification (not fixed delays alone), without flicker or repeated widget rebuilds.
- **Absent target behavior**: if the selected monitor is not connected, do not cover another screen by default. The window hides (or stays minimized to a tray/notification state) and logs why; when the monitor reappears it is placed automatically. A setting allows "fall back to primary" for users who want it.
- The existing selectors (`tallest`, `widest`, `largest`, `primary`) remain as advanced/legacy options.

## Capabilities

### New Capabilities
- `display-targeting`: monitor discovery and identification, stable persisted selection, picker UI, robust placement across display changes, and absent-monitor behavior.

### Modified Capabilities
- `host-shell` (defined in the unarchived `urdeck-framework` change): its "Monitor Selection" and "WPF Host Window" requirements are extended. To be reconciled when both changes are archived.

## Impact

- **Code**: `UrDeck.Host` (`MonitorPlacement`, `MainWindow`, new picker/identify UI), `UrDeck.Engine` (`UrDeckConfig`: stable monitor identity fields, migration).
- **Config**: new stable identity field(s) alongside `monitorName`/`monitor`; legacy values still honored.
- **Dependencies**: uses Win32 display APIs (display config / EDID) via P/Invoke; no new NuGet dependencies expected.
- **Memory/CPU**: the picker is on-demand; idle behavior unchanged. A configuration surface adds WPF UI, so its cost must be measured against the memory goal.
- **Non-goals**: multiple simultaneous dashboards on several monitors, per-monitor page assignment, remote/virtual display support.
