# Display Targeting — Tasks

## 1. Discovery and identity

- [ ] 1.1 Extend monitor enumeration with device path, EDID manufacturer/product/serial and friendly name (Win32 display APIs)
- [ ] 1.2 Add `monitorId` (and optional `fallbackToPrimary`) to `WidgyConfig`; matching order: device path, EDID triple, legacy name/index, primary if enabled
- [ ] 1.3 Tie-break for identical displays using previous position and size
- [ ] 1.4 Abstract enumeration and placement behind an interface so selection and convergence can be unit-tested
- [ ] 1.5 Unit tests: identity matching, legacy config, re-enumeration order changes, duplicates

## 2. Convergence

- [ ] 2.1 Replace fixed 0/1.5/5s retargeting with a settle-then-verify state machine with capped backoff
- [ ] 2.2 Verify window bounds and DPI against the target display after each attempt; log every transition
- [ ] 2.3 Coalesce layout rebuilds to one per settled state
- [ ] 2.4 Simulated tests: sleep/wake with displays returning in different orders, hot-plug, mixed DPI, DPI change

## 3. Absent target behavior

- [ ] 3.1 Hide the window (keep the process running) when the selected display is absent and fallback is off; place it automatically when the display returns
- [ ] 3.2 Pause widget timers while the window is hidden and resume on show
- [ ] 3.3 Implement the `fallbackToPrimary` option

## 4. Picker UI

- [ ] 4.1 Picker window listing displays with friendly name, resolution, orientation, primary flag and position
- [ ] 4.2 Identify action showing a marker on each physical display
- [ ] 4.3 Selecting a display retargets immediately and persists `monitorId`
- [ ] 4.4 Decide and implement how the picker is opened (command-line switch, tray icon or hotkey)
- [ ] 4.5 Verify the picker is released after closing and does not raise idle memory or CPU

## 5. Verification

- [ ] 5.1 Manual test on the 1100x3840 panel: sleep/resume, unplug/replug, reboot with cables swapped, mixed-DPI with a scaled primary
- [ ] 5.2 Update README configuration section with `monitorId` and the picker
