## 1. Baseline

- [ ] 1.1 Branch off `main`; confirm `dotnet build urdeck.slnx -c Release` (0 warnings) and `dotnet test urdeck.slnx -c Release` pass before any change
- [ ] 1.2 Capture the "before" picture: `UrDeck.Host.exe --snapshot before.png --size 1100x3840` with a 4x2, 4x1, 2x1 and 1x1 Clock on the page; keep it outside the repo
- [ ] 1.3 Record baseline private bytes (`URDECK_MEMLOG=1`) for the same page

## 2. SDK contract

- [ ] 2.1 Add the resolved `Theme` class to `UrDeck.Sdk` (colours, card values in pixels, small-text sizes in pixels, unit ratio, stroke ratio and cap, a typeface per text role, `IDisposable`) and the `TextRole` enum (design decision 1)
- [ ] 2.2 Change `WidgetRenderContext`: `Theme` becomes the new type, add `ContentRect`, update the constructor; delete `ThemeColors`
- [ ] 2.3 Bump the SDK `AssemblyVersion` to 0.2.0.0 and update `SdkContractTests` and the csproj comment
- [ ] 2.4 Fix every compile error this causes in engine, host, Clock and tests with temporary default values so the solution builds again

## 3. Theme loading (engine)

- [ ] 3.1 Add `ThemeDefinition` (all members nullable) and its JSON shape; document the settings file fields in a short `docs/themes.md`
- [ ] 3.2 Choose and add the bundled font: confirm Inter variable is OFL 1.1 from its upstream release, add the font and its licence text, embed it in `UrDeck.Engine`
- [ ] 3.3 Add the embedded `default-dark` and `default-light` theme settings (design decision 10)
- [ ] 3.4 Implement the theme store: discover `themes/<name>/theme.json`, user over built-in, merge over `default-dark`, per-value and per-theme fallback with warnings, reject font paths that leave the theme folder
- [ ] 3.5 Implement the resolver: definition plus physical cell size to `Theme`; load the typeface once per role, applying weight through the variable-font axis (check the SkiaSharp 4.153 API through Context7 first) with the fallback chain from the `theme` spec
- [ ] 3.6 Tests: partial theme merge, unknown name, unreadable file, invalid colour, font path escape, missing font, sizes double when the cell size doubles, built-ins available with no themes folder

## 4. Layout gap

- [ ] 4.1 `GridLayoutManager` takes the gap fraction and returns the card rectangle per widget, keeping the cell rectangle available; gap 0 equals today's output
- [ ] 4.2 Update and extend layout tests for the scenarios in the `grid-layout` delta (gap between neighbours, gap independent of widget size, zero gap)

## 5. Card painting (engine)

- [ ] 5.1 In `WidgetPainter.RenderSafely`: draw the card fill and border, clip to the rounded rectangle, render the widget, restore; clamp the radius to half the smaller side
- [ ] 5.2 Replace the error tile with the themed error card
- [ ] 5.3 `PageRenderer` takes the definition or theme, clears to the theme background, uses card rectangles and the shared painter
- [ ] 5.4 Tests on rendered bitmaps: empty widget shows the card fill; corner pixel is transparent with a radius and filled with radius 0; edge-to-edge content is clipped to the corners; a throwing widget produces critical-coloured pixels inside the card; 4x2 and 1x1 cards have the same radius
- [ ] 5.5 Measure paint time for a 1100x550 card with and without the antialiased clip; write the numbers to `docs/perf/`; stop and report if the clip costs more than about 1 ms per paint

## 6. Host wiring

- [ ] 6.1 Select and resolve the theme on startup and on config reload; dispose the previous `Theme` after the rebuild
- [ ] 6.2 Window background from the theme; elements placed at card rectangles; resolve against the physical cell size
- [ ] 6.3 Include the DPI scale and the active theme in the "layout changed" check so a scaling or theme change rebuilds the page
- [ ] 6.4 `--snapshot` uses the configured theme and accepts `--theme <name>`
- [ ] 6.5 Copy nothing new into the output for built-in themes; confirm a `themes/` folder next to the exe is picked up when present

## 7. Components (SDK)

- [ ] 7.1 Implement `Readout` in `UrDeck.Sdk.Components` per design decision 8, including the widest-value sample, equal-width digits, raised and baseline units, label, scale and alignment
- [ ] 7.2 Implement `TextLine` with the three steps, emphasis, alignment and shrink-only fitting
- [ ] 7.3 Tests: equal rectangles with the same sample give the same size for `8` and `100`; changing digits does not change size or move the colon; a fitting label has the same pixel size in a 1x1 and a 4x4 card; an over-wide title shrinks to the rectangle; text never exceeds the step size

## 8. Clock migration

- [ ] 8.1 Rewrite `ClockWidget.Render` on `ContentRect`, `Readout` and `TextLine` (design decision 9); remove `DrawPanel`, `FitWidth`, `CapHeight` and the static typefaces
- [ ] 8.2 Update `ClockWidgetTests` and add cases for the 12-hour unit, hidden date, `fontSize` below 1 and the `textColor` override
- [ ] 8.3 Confirm `NeedsRender` behaviour is unchanged

## 9. Verification

- [ ] 9.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; coverage run for `UrDeck.Engine.Tests` stays above the floor; `dotnet format urdeck.slnx --severity warn --verify-no-changes`
- [ ] 9.2 `openspec validate --all --strict`
- [ ] 9.3 Snapshots at 1100x3840 for `default-dark` and `default-light` (`--theme`); compare with the "before" picture and give all three to the owner
- [ ] 9.4 Add a throwaway user theme with only an accent colour and radius 0; confirm flat tiles in a snapshot, then delete it
- [ ] 9.5 Run on the panel (detached); check `urdeck.log` for theme selection and warnings; change `theme` in the config and confirm the switch without restart; ask the owner to look
- [ ] 9.6 Record private bytes after the change and compare with task 1.3 against the 60-70 MB budget; confirm idle CPU is unchanged

## 10. Documentation

- [ ] 10.1 `README.md`: `theme` key is now used, themes folder, how to make a user theme, licence map entry for the bundled font
- [ ] 10.2 `CONTRIBUTING.md`: update the widget-authoring example to `ContentRect`, `Readout` and `TextLine`; state that widgets draw no card and name no font
- [ ] 10.3 `AGENTS.md`: update the SDK layout line and the "planned theme engine" note
- [ ] 10.4 `docs/ROADMAP.md`: mark item 5 done, and add the follow-up items (animation and frame requests with the GPU question, gauge, glyphs and icons, image tile) with a pointer to this change's design
