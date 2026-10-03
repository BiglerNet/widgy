# Handoff: explore data providers (2026-10-03)

Context for a fresh session (Opus) that will run `/opsx:explore` and then `/opsx:propose` for roadmap item 3. Read
`AGENTS.md`, `README.md` and `docs/ROADMAP.md` (items 3, 4, 7 and 14) first. Item 3 already holds the design direction
(topics, providers as a plugin kind, demand-driven polling, adaptive refresh); this note adds the first consumer, what
exists in the code, and the questions that direction leaves open.

## Start here

1. `/model opus`, then ask: "Read `docs/handoff/2026-10-03-data-providers.md`, then run `/opsx:explore` on data
   providers with me."
2. Explore with the owner (conversation, no code). The owner has said that existing design, docs and agent guidance are
   suggestions: challenging the direction in item 3 is welcome when the design is better for it.
3. `/opsx:propose` a new change (suggested name: `data-providers`).
4. Implement with `/opsx:apply` in a fresh Sonnet session, review on Opus, then `/opsx:archive`. One PR per change.

If the animation exploration (item 14) has already happened, read its change first: it may have changed how a widget
asks to be repainted, which is the mechanism a "topic changed" signal has to use.

## The first consumer: the performance widget

Designing providers without a consumer risks freezing the wrong API. The owner described the widget they want, from
their current HYTE Nexus page (the look is to be UrDeck's own, the content is the reference):

| Size | Content |
|---|---|
| 2x2 | one stat |
| 4x2 | two stat gauges and three text stats |
| 4x4 | four stat gauges (CPU temperature, CPU load, GPU temperature, GPU load) and three text stats (memory percent, GPU power in watts, GPU clock in MHz) |

A second pattern the owner wants: **a row of four 1x1 cards, one CPU core each**, where each card is the same widget
type configured to point at a different core and labelled "Core 1" to "Core 4". So a reading must be addressable per
instance (per core, per GPU, per drive), not only per machine.

Conventions already settled for every widget (roadmap item 7):

- A label belongs to the reading. Its text is a per-widget setting with three states: not set (the widget picks a
  default from what it points at), text (the user's override) and empty (hidden). A widget with several readings has
  one such setting per reading.
- A widget picks its composition from its grid size; components draw into whatever rectangle they are given.
- Each widget owns its configuration type; a per-widget editor UI belongs to roadmap item 11.

## What exists today (verified against `main` at the merge of #9)

- **No shared data at all.** Each widget fetches its own data in `IWidget.UpdateAsync`, called by its own timer in
  `src/UrDeck.Host/WidgetView.cs`. There is no event bus, topic store or provider concept in the code.
- **Refresh attributes** (`sdk/UrDeck.Sdk/Attributes/`): a widget declares exactly one of `[RefreshOnTick]`,
  `[RefreshAdaptive(minMs, maxMs)]` (runs at `minMs`; no load-based scaling yet) or `[RefreshOnEvent(eventName)]`
  (the name is stored in the widget descriptor and nothing raises it; the widget renders once). The analyzer
  (URDECK003, URDECK004) and the loader both enforce "exactly one".
- **Render skipping:** after each refresh the host asks `NeedsRender(now)` and repaints only when it returns true.
- **Plugins** load into collectible `AssemblyLoadContext`s from a shadow copy
  (`src/UrDeck.Engine/Plugin/PluginLoadContext.cs`). Any assembly the host can supply is shared, so types defined in
  `UrDeck.Sdk` have one identity across plugins; types defined inside a plugin do not. The loader only looks for
  widgets today.
- **Widgets have no logging and no host services.** The roadmap parks `ILogger` and a host-services hook as "do when
  the first widget needs it"; a provider that talks to hardware is likely that moment.
- **Drawing is ready for readings.** `UrDeck.Sdk.Components.Readout` draws a value, a unit (raised or on the baseline)
  and a label, and takes a `WidestValue` so the text size does not change as the number changes. `Theme` already carries
  `Accent`, `AccentDim`, `Good`, `Warning`, `Critical`, `StrokeRatio` and `StrokeCap` for the gauge. The gauge itself is
  not built; it arrives with the performance widget.
- The SDK assembly version is 0.2.0.0; the SDK is unpublished with one in-repo consumer.

## Open questions for the exploration

1. **Scope of the first change.** Provider infrastructure alone has no visible result. Options: infrastructure plus a
   system provider plus a minimal single-stat widget (1x1 and 2x2, readout only), with the gauge and the full
   performance widget as the next change; or everything in one change. The owner prefers small changes.
2. **Addressing instances.** How "CPU core 2" or "GPU 0 temperature" is named: one topic per instance, or one topic
   with a structured value and a selector in the widget's config. This decides what the per-core cards point at, and
   how a widget offers the user a list of things to point at (which the editor will need later).
3. **Payload types.** Standard reading types in the SDK (a number with a unit, a minimum and maximum, a timestamp) versus
   free-form values. Type identity across plugin load contexts is the hard part; item 3 suggests SDK types plus a
   generic schema'd value for community topics.
4. **Units and formatting.** Who turns 72.6 into "72.6" and "W": the provider, the widget or a shared formatter. Celsius
   versus Fahrenheit is a user preference that must not be decided per widget.
5. **Unavailable and stale readings.** What a readout shows when a sensor is missing, needs rights the process lacks or
   has stopped updating, so that every widget shows it the same way.
6. **Where sensor data comes from.** Load and memory are available without special rights through Windows APIs.
   Temperatures, power and clocks usually need LibreHardwareMonitor or similar. To verify before choosing: its licence
   and compatibility with GPL-3.0-or-later, whether it needs administrator rights or a kernel driver, whether that
   driver is flagged by Windows security tools, and what it costs in memory. A provider that needs elevation may have
   to run as a separate process, which is a design in itself.
7. **Scheduling and fault containment.** Engine-owned polling at the fastest rate any subscriber needs; a provider that
   is slow, throws or hangs must not affect the UI thread or other providers. Where sampling runs (not the UI thread)
   and how the latest value reaches a widget at draw time without locks on the render path.
8. **How a topic change repaints a widget.** It replaces the bare `[RefreshOnEvent("name")]`. It has to fit with
   `NeedsRender` and with whatever the animation change (item 14) decides about asking for frames.
9. **Adaptive refresh.** Item 3 makes `[RefreshAdaptive]` a consumer of `system.cpu` (back off when the PC is busy or on
   battery). The animation exploration lists the same need for frame rates. Decide which change owns it.
10. **History.** Charts are parked, so the first version needs no history buffers. Confirm that, and note where history
    would live later (provider or widget) so the first API does not rule it out.
11. **Prerequisites from the roadmap's "Parked" list.** Which of loader hardening, SDK versioning, public API tracking
    and widget logging must land before or with a second plugin kind.

## Constraints and preferences

- Keep the first implementation minimal; record the rest in the roadmap.
- Performance is priority one: idle CPU near zero, a 60-70 MB private budget for the host. Sampling must stop when no
  widget is subscribed. Measure what a provider adds (`URDECK_MEMLOG=1`, `docs/perf/`).
- Widgets and providers reference only `UrDeck.Sdk` (MIT), never `UrDeck.Engine` or `UrDeck.Host`. A new plugin kind
  should be named in `PLUGIN-EXCEPTION.md`.
- A breaking SDK change is acceptable while the SDK is unpublished, if it gives a cleaner contract; bump the frozen
  assembly version deliberately.
- Third-party libraries need a licence check, their licence text and an attribution entry.
- Use Context7 for library documentation, as the owner's global rules require.
- Delegate implementation to Sonnet; keep Opus for design and review. One fresh session per exploration.

## Pointers

`docs/ROADMAP.md` (item 3 for the direction, item 7 for the widget table and conventions, "Parked" under item 1,
"Suggested order"), `docs/handoff/2026-10-03-animation-and-rendering.md`, `openspec/specs/widget-sdk/spec.md`
("Refresh Policy System", "Render Skipping", "Plugin Isolation and Hot-Reload"), `openspec/specs/components/spec.md`,
`openspec/specs/theme/spec.md`, `src/UrDeck.Host/WidgetView.cs`, `src/UrDeck.Engine/Plugin/WidgetPluginLoader.cs`,
`src/UrDeck.Engine/Plugin/WidgetRegistry.cs`, `sdk/UrDeck.Sdk/Components/Readout.cs`, `PLUGIN-EXCEPTION.md`.
