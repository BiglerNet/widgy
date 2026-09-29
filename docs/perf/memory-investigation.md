# Widgy.Host memory investigation

## Context

- Windows 11 Pro 10.0.26200, .NET 10, Release build, one Clock widget (4x2 grid, 1100x550 DIPs, scale 1.5) on the 1100x3840 portrait monitor.
- Method: launch detached, wait 13 s, read `WorkingSet64` / `PrivateMemorySize64` from the process, plus GC numbers logged 10 s after start. Each row was run twice; values are the (near identical) results. Env-var experiments use `DOTNET_*` on the launched process only.
- Opt-in diagnostics: `WIDGY_MEMLOG=1` logs `MEM ...` to `widgy.log` 10 s after start.

## Results

| Config | WS MB | Private MB | GC heap MB | Notes |
|---|---|---|---|---|
| Baseline (hardware WPF render) | 139 | 128 | 1.5 (5 committed) | 26-27 threads |
| Empty page (no widgets), hardware | 112-113 | 104 | 0 | WPF window alone; widget costs ~24 MB |
| **SoftwareOnly render mode** | **115** | **66** | 1.5 | -62 MB private, -24 MB WS |
| Empty page + SoftwareOnly | 99 | 53 | 0 | practical floor for a WPF window |
| DOTNET_GCgen0size=0x400000 | 136-138 | 128 | 1.5 | no effect |
| DOTNET_GCConserveMemory=7 | 138-139 | 127-128 | 1.5 | no effect |
| DOTNET_gcConcurrent=0 | 138-139 | 125-127 | 1.4 | no effect (noise) |
| TieredPGO=0 (+ QuickJitForLoops) | 135 | 125-128 | 1.5 | no effect |
| SW + TieredCompilation=0 | 116 | 66 | 1.4 | no effect |
| SW + DOTNET_ReadyToRun=0 (JIT everything) | 128 | 78 | 1.5 | shows R2R framework is already helping (+12 MB if disabled) |
| Published with PublishReadyToRun, hardware | 138 | 128 | 1.4 | no effect (app code is ~100 KB; framework already R2R) |
| Published R2R + SW | 117 | 66 | 1.4 | same as SW alone |
| SW + concurrent GC off + ConserveMemory + PGO off | 115 | 65 | 1.5 | GC knobs add nothing on top of SW |

Idle CPU (one core = 100%, 30 s window, clock ticking once a second): hardware 0.7%, software 1.3%. So software mode costs roughly +0.6% of one core (about 0.04% of a 16-thread machine's total capacity) in exchange for 62 MB.

## Findings

1. The managed heap is not the problem: GC heap is 1.5 MB (5 MB committed). GC tuning (gen0size, ConserveMemory, non-concurrent, PGO, tiered compilation) changes nothing measurable; do not add it.
2. Almost all memory is native. About 100 MB private of the baseline is WPF/milcore/D3D plus the CLR itself. Hardware composition of a 1100x3840 window (`wpfgfx_cor3.dll` render targets, D3D9 driver state) accounts for ~50-60 MB of private bytes; `RenderOptions.ProcessRenderMode = SoftwareOnly` removes it. The widget adds ~24 MB (hardware) or ~13 MB (software): the WriteableBitmap backing the SKElement (1650x825x4 = 5.4 MB physical) plus Skia/typeface caches and libSkiaSharp.
3. OpenTK / GLWpfControl are copied to the output (SkiaSharp.Views.WPF depends on them) but are NOT loaded at runtime: no OpenTK*, GLWpf or opengl32 module or assembly is present in the process. They only cost disk space.
4. R2R publishing does not help; the framework is already R2R and Widgy's own code is tiny. Trimming is unsupported for WPF (not tried).
5. Working set includes shared mapped DLL pages (about 50 MB, "free" when other WPF apps run); private bytes is the fairer number.

## Is 50 MB realistic?

An empty WPF window on this machine is 53 MB private / 99 MB working set even in software mode, before any widget. With one widget the best achieved is 66 MB private / 115 MB WS. Under 50 MB private is not achievable with WPF; a target of < 50 MB should be restated as private bytes ~65 MB per process with WPF, or would require dropping WPF for a raw Win32 window plus Skia (a rendering-architecture change, likely landing around 20-30 MB). Working set below 100 MB is not possible with WPF.

## Recommendations (impact vs. risk)

1. **Applied**: `RenderOptions.ProcessRenderMode = SoftwareOnly` (-62 MB private, -24 MB WS; cost ~0.6% of a core while ticking; opt out with `WIDGY_HWRENDER=1`). Low risk, since widgets already render in software.
2. Not worth doing: GC settings, TieredPGO, ReadyToRun, ConserveMemory (all measured as no-ops).
3. Medium effort, unmeasured: cap SKElement bitmap to the widget's real area (already the case) and render at 1.0 DPI for low-detail widgets; each 1650x825 widget is 5.4 MB, so many widgets scale linearly (~13 MB each including caches). Consider sharing typefaces/paints across widgets.
4. High impact, high effort: replace WPF with a bare Win32 layered/child window plus Skia, or a single SKElement for the whole page instead of one per widget. This is the only route to the < 50 MB proposal target.
5. Remove OpenTK/GLWpfControl from the output if a SkiaSharp.Views.WPF build without them is available (disk only).
