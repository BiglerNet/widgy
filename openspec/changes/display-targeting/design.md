# Display Targeting — Design

## Context

`MonitorPlacement` enumerates monitors with `EnumDisplayMonitors`/`GetMonitorInfo` (physical pixels) and `Select` picks one from `monitorName`/`monitor`. `MainWindow` covers the target with `SetWindowPos`, re-applies after `DpiChanged`, and retargets on `SystemEvents.DisplaySettingsChanged`, power resume (+1.5s, +5s) and config changes. The process is PerMonitorV2 DPI aware.

## Decisions

1. **Identity.** Extend enumeration to fetch the display device path and EDID-derived manufacturer/product/serial (via `EnumDisplayDevices` with `EDD_GET_DEVICE_INTERFACE_NAME`, and/or the CCD APIs `QueryDisplayConfig`/`DisplayConfigGetDeviceInfo` for the friendly name). Persist a `monitorId` (device path, plus manufacturer/product/serial as a secondary match) next to legacy keys. Matching order: exact device path, then EDID triple, then legacy name/index, then (if enabled) primary.
2. **Picker.** A small WPF window opened from a command-line switch or tray/context entry (exact entry point decided during implementation); Identify uses short-lived borderless topmost windows per display. On selection: write config, retarget immediately.
3. **Convergence instead of fixed delays.** Replace the 0/1.5/5s schedule with a state machine: on any display signal (settings changed, resume, DPI changed, config changed) start/extend a short settle timer, then enumerate, select, cover, and *verify* (window bounds equal target bounds and DPI matches the monitor); if verification fails, retry with backoff up to a cap. Layout rebuild happens once per settled state.
4. **Absent target.** Window is hidden (not closed) while no matching display exists; a display-change signal re-runs selection. The primary fallback is an opt-in config flag (`fallbackToPrimary`), default off.
5. **Migration.** First save of a picker selection writes `monitorId` and leaves `monitorName`/`monitor` untouched so older builds keep working.

## Risks / Open Questions

- EDID serials are often missing or duplicated on identical panels; the tie-break by previous position is heuristic.
- The HYTE Y70 Touch panel may report a generic EDID name; verify on hardware and design the fallback naming.
- Hidden-window behavior with per-widget timers running: timers should pause while hidden to keep idle CPU near zero.
- Picker UI adds WPF surface area and memory; keep it lazy and released after closing (relates to the memory goal in `urdeck-framework`).
- Verification of "correct scale" needs a testable seam: abstract the monitor enumeration/placement behind an interface so convergence logic can be unit-tested with simulated display sequences (sleep/wake orders, DPI changes).
