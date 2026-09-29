# Widgy Framework — Implementation Tasks

## 1. Project Structure Setup

- [x] 1.1 Create `.gitignore` file with .NET 8 + WPF + SkiaSharp ignores (obj, bin, packages, NuGet, .vs, .idea, *.user, node_modules, widgy-config.json)
- [x] 1.2 Create `Widgy.Host/` directory and `.csproj` (WPF app, TargetFramework net8.0-windows, output exe)
- [x] 1.3 Create `Widgy.Core/` directory and `.csproj` (class library, TargetFramework net8.0, output dll)
- [x] 1.4 Create `Widgy.Analyzer/` directory and `.csproj` (Analyzer, TargetFramework netstandard2.0, analyzer reference)
- [x] 1.5 Create `Widgy.Widgets.Clock/` directory and `.csproj` (class library, TargetFramework net8.0, output dll)
- [x] 1.6 Create `widgy.sln` solution with all 4 projects
- [x] 1.7 Create `plugins/` directory (where runtime plugin DLLs land)
- [x] 1.8 Add project reference from Widgy.Host → Widgy.Core
- [x] 1.9 Add project reference from Widgy.Analyzer as reference (not reference)
- [x] 1.10 Add project reference from Widgy.Widgets.Clock → Widgy.Core
- [x] 1.11 Add SkiaSharp and SkiaSharp.Views.WPF NuGet packages to Widgy.Host and Widgy.Core
- [x] 1.12 Add Microsoft.CodeAnalysis.Analyzers and Microsoft.CodeAnalysis.CSharp packages to Widgy.Analyzer
- [x] 1.13 Build all projects to verify compilation succeeds without errors

## 2. Core Widget SDK (Widgy.Core)

### 2.1: Widget Metadata Classes

- [x] 2.1.1 Create `Widgy.Core/Attributes/WidgetAttribute.cs` — `[Widget(name, description)]` attribute with Name, Description, Optional properties for category and configType
- [x] 2.1.2 Create `Widgy.Core/Attributes/WidgetSizeAttribute.cs` — `[WidgetSize(width, height)]` attribute with Width and Height properties (supports multiple attributes on same class)
- [x] 2.1.3 Create `Widgy.Core/Attributes/RefreshOnTickAttribute.cs` — `[RefreshOnTick(interval, TimeUnit)]` attribute with Interval and Unit properties
- [x] 2.1.4 Create `Widgy.Core/Attributes/RefreshAdaptiveAttribute.cs` — `[RefreshAdaptive(minMs, maxMs)]` with MinMs and MaxMs properties
- [x] 2.1.5 Create `Widgy.Core/Attributes/RefreshOnEventAttribute.cs` — `[RefreshOnEvent(eventName)]` with EventName property
- [x] 2.1.6 Create `Widgy.Core/Attributes/CategoryAttribute.cs` — `[Category(name)]` attribute with Name property
- [x] 2.1.7 Create `Widgy.Core/Attributes/RefreshStrategy.cs` — enum: None, OnTick, OnEvent, Adaptive (only one can be non-None)
- [x] 2.1.8 Create `Widgy.Core/Enums/TimeUnit.cs` — enum: Seconds, Minutes, Hours, Milliseconds
- [x] 2.1.9 Create `Widgy.Core/Config/WidgetConfig.cs` — base class with WidgetTypeId, Col, Row, Width (int), Height (int), Parameters (dictionary<string, string>), IsVisible, SaveConfigJson(), LoadConfigJson(string)
- [x] 2.1.10 Create `Widgy.Core/Widget<TConfig>` — abstract base class for all widgets with abstract RenderAsync method

### 2.2: Widget Interface and Runtime Context

- [x] 2.2.1 Create `Widgy.Core/Interfaces/IWidget.cs` — `IWidget<TConfig> where TConfig : WidgetConfig, new()` with Name, Description, Category, SupportedSizes (Size[]), DefaultConfig (TConfig), ConfigType (Type)
- [x] 2.2.2 Create `Widgy.Core/Rendering/WidgetRenderContext.cs` — class with Canvas (SKCanvas), Time (DateTime), PixelSize (Size), Theme (ThemeColors), Config (WidgetConfig), CancellationToken (CancellationToken)
- [x] 2.2.3 Create `Widgy.Core/Rendering/ThemeColors.cs` — struct with TextColor (SKColor), BackgroundColor (SKColor), AccentColor (SKColor), PanelBackgroundColor (SKColor), PanelHeaderColor (SKColor)
- [x] 2.2.4 Verify all core types compile and expose public APIs via `dotnet build Widgy.Core`
- [x] 2.2.5 ConfigStore for widgy-config.json with hot-reload via FileSystemWatcher (debounce 1000ms)

## 3. Code Analyzer (Widgy.Analyzer)

- [x] 3.1 Create `Widgy.Analyzer/WidgetAnalyzer.cs` — inherits from Microsoft.CodeAnalysis.DiagnosticAnalyzer, registers for `[TypeDeclaration]` with base class `Widget<TConfig>`, checks for required attributes, reports diagnostics with IDs: WIDGY001 (missing [Widget]), WIDGY002 (missing [WidgetSize]), WIDGY003 (invalid refresh strategy), WIDGY004 (conflicting refresh strategies), WIDGY005 (invalid widget size values)
- [x] 3.2 Implement analysis logic: check `[Widget]` exists (error WIDGY001), check at least one `[WidgetSize]` exists (error WIDGY002), check exactly one refresh strategy attribute exists (error WIDGY003 for none, error WIDGY004 for multiple), validate size values are 1-4 width and 1-N height (error WIDGY005)
- [x] 3.3 Create `Widgy.Analyzer/AnalyzerReleases.Shipped.md` and `Widgy.Analyzer/AnalyzerReleases.Unshipped.md` (required for Roslyn analyzers)
- [x] 3.4 Build Widgy.Analyzer to verify it compiles (output should be a .dll analyzer assembly)
- [x] 3.5 Verify analyzer emits diagnostics by adding a broken widget to Widgy.Widgets.Clock/ and checking build output

## 4. Plugin Loader (Widgy.Core)

- [x] 4.1 Create `Widgy.Core/Plugin/WidgetPluginLoader.cs` — class that scans a plugin directory for DLLs, loads them via Assembly.LoadFrom, finds IWidget implementations via reflection, registers in WidgetRegistry
- [x] 4.2 Implement reflection scanning logic: iterate Assembly.GetTypes(), find types where type.IsClass + !type.IsAbstract + type.GetInterface("IWidget") + GetCustomAttribute<WidgetAttribute>(), extract metadata
- [x] 4.3 Create `Widgy.Core/Plugin/IPluginService.cs` — interface with ScanAndLoadPlugins(string pluginDirectory), ReloadPlugins(), GetRegisteredWidgetTypes(), GetWidgetInstance(string typeId, WidgetConfig config)
- [x] 4.4 Create `Widgy.Core/Plugin/WidgetRegistry.cs` — in-memory registry mapping widget type IDs to their registered IWidget<TConfig> types and cached instances
- [x] 4.5 Implement hot-reload detection: FileSystemWatcher on plugin directory, debounce file change events by 500ms, re-scan on valid change
- [x] 4.6 Verify plugin loader correctly loads types that implement IWidget and reads their metadata attributes

## 5. Grid Layout Engine (Widgy.Core)

- [x] 5.1 Create `Widgy.Core/Layout/GridCell.cs` — class with Col (int), Row (int), Width (int), Height (int) plus helper methods
- [x] 5.2 Create `Widgy.Core/Layout/GridLayoutManager.cs` — class with constructor(GridLayoutManager(screenWidth, screenHeight), ColumnWidth (property), RowHeight (property), ConvertToPixels(Size, position) method, ValidatePosition(int col, int width) method, RenderWidgetLayout(List<WidgetConfig> widgets, Size screenDims) method returning List<WidgetLayoutItem>)
- [x] 5.3 Create `Widgy.Core/Layout/WidgetLayoutItem.cs` — class with WidgetTypeId (string), Position (Point), Size (Size), GridSize (Size)
- [x] 5.4 Implement layout validation: Col + Width <= 4, width >= 1 && <= 4, height >= 1, Col >= 0. If validation fails, log warning and clamp to valid range)
- [x] 5.5 Implement monitor-aware grid system: MonitorChangedEvent (Size newScreenDims) event, RecalculateColumns() method that updates ColumnWidth and RowHeight
- [x] 5.6 Verify grid correctly converts grid units to pixels for multiple monitor sizes (1100x3840, 3840x2160, 7680x2160)

## 6. Host Shell (Widgy.Host)

- [x] 6.1 Create `Widgy.Host/MainWindow.xaml` — WPF window with WindowStyle.None, WindowState="Maximized", Background (solid color), Grid child control
- [x] 6.2 Create `Widgy.Host/MainWindow.xaml.cs` — code-behind with OnResize(), OnMonitorChanged(), OnClose() lifecycle methods, initialization logic
- [x] 6.3 Create `Widgy.Host/App.xaml` and `Widgy.Host/App.xaml.cs` — application entry point with OnStartup() override: monitor detection → config load → plugin scan → grid init → show main window
- [x] 6.4 Create `Widgy.Host/Resources/DefaultTheme.xaml` — WPF resource dictionary with default dark theme colors (Text, Background, Accent, Accent)
- [x] 6.5 Implement monitor detection: use System.Windows.Forms.Screen.PrimaryScreen to find active monitor(s) and compute work area
- [x] 6.6 Implement plugin directory setup: during App.OnStartup(), create `./plugins/` if it does not exist
- [x] 6.6 Implement configuration management: Widgy.Core/Config/ConfigStore.cs with class that reads/writes widgy-config.json (using System.Text.Json), implements hot-reload via FileSystemWatcher on the config file, debounce 1000ms
- [x] 6.7 Add SkiaSharp integration: In MainWindow.xaml.cs, add WriteableBitmap per widget with SkiaSharp rasterization for widget lifecycle (start/stop per widget)
- [x] 6.8 Implement widget rendering loop: RenderAsync() per active widget, each bitmap sized to its grid cell position
- [x] 6.9 Implement window sizing: on startup, set Window to primary monitor work area. On monitor change, update to new work area.

## 7. Built-in Clock Widget (Widgy.Widgets.Clock)

- [x] 7.1 Create `Widgy.Widgets.Clock/ClockConfig.cs` — class extending WidgetConfig with properties Format (string, default "24h"), ShowDate (bool, default true), TextColor (string? null), FontSize (double, default 1.0)
- [x] 7.2 Create `Widgy.Widgets.Clock/ClockWidget.cs` — class implementing IWidget<ClockConfig>, decorated with [Widget("Clock", "Display current time")], [WidgetSize(4, 2)], [RefreshOnTick(1, TimeUnit.Seconds)], [Category("System")]
- [x] 7.2.3 Implement IWidget<ClockConfig>: Name → "Clock", Description → "Display current time", Category → "System", SupportedSizes → new[]{new Size(4, 2), new Size(1, 1)}, DefaultConfig, ConfigType
- [x] 7.2.4 Implement RenderAsync: GetSKCanvas, compute font sizes proportional to canvas height (font-size ≈ canvas.height * 0.3 for time, canvas.height * 0.15 for date). Center text horizontally and vertically. Use SKPaint with SKTextAlign.Center and SKTextSize.Middle. Set color from WidgetRenderContext.Theme.TextColor.
- [x] 7.2.5 Add 24-hour/12-hour format support: if ClockConfig.Format == "12h", use DateTime.ToString("h:mm tt"), else use DateTime.ToString("HH:mm")
- [x] 7.2.6 Add date line support: if ClockConfig.ShowDate is true, render DateTime.ToString("ddd MMM d, yyyy") below the time line
- [x] 7.2.7 Add text scaling: calculate font sizes based on available canvas height (scale factor from ClockConfig.FontSize)
- [x] 7.2.8 Draw time and date strings centered in the canvas
- [x] 7.2.9 Add background rendering: SKCanvas draw rectangle covering entire canvas with WidgetRenderContext.Theme.BackgroundColor
- [x] 7.3 Create `Widgy.Widgets.Clock/Widgy.Widgets.Clock.csproj` — class library, output dll, Target net10.0
- [x] 7.4 Build Widgy.Widgets.Clock to verify compilation succeeds
- [x] 7.5 Copy the output DLL to `Widgy.Host/bin/Debug/net8.0-windows/plugins/Widgy.Widgets.Clock.dll`

## 8. End-to-End Integration

- [x] 8.1 Create `Widgy.Host/widgy-config.json` with default page containing Clock widget at col:0, row:0, width:4, height:2, Format "24h", ShowDate true
- [ ] 8.2 Build Widgy.Host and copy Widgy.Widgets.Clock.dll to plugins/ directory
- [ ] 8.3 Run Widgy.Host.exe and verify window opens on the target monitor, showing a centered digital clock
- [ ] 8.4 Verify the clock updates every second
- [ ] 8.4.5 Verify the grid layout adapts to 1100x3840 vertical panel (widget fills full width, 2 rows tall)
- [ ] 8.5 Test hot-reload: change widgy-config.json and verify the host picks up the new configuration
- [ ] 8.6 Test hot-reload: change Widgy.Widgets.Clock.dll in plugins/ and verify the host detects the plugin change
- [ ] 8.7 Test plugin load failure: remove Widgy.Widgets.Clock.dll and verify the host starts cleanly with an empty page
- [ ] 8.8 Test window resizing: manually resize the window to a different resolution and verify the grid recalculates

## 9. Final Polish

- [ ] 9.1 Add application icon (.ico)
- [ ] 9.2 Add startup icon to widgy.config.json
- [ ] 9.3 Remove debug output in production build (remove Console.Write lines)
- [ ] 9.4 Test with actual display hardware if available (1100x3840)
- [ ] 9.5 Verify Widgy.sln builds in Release mode without errors
- [ ] 9.6 Add Widgy.Host/Resources/DefaultTheme.xaml for application-wide styles (font family, etc.)
- [ ] 9.7 Add README.md to project root explaining how to build and run
- [ ] 9.8 Add widgy-config.json to .gitignore
- [ ] 9.9 Add Widgy.Analyzer as a reference in Widgy.Widgets.Clock.csproj (for compile-time validation during development)
- [ ] 9.10 Verify all open questions from design.md are resolved or documented in the codebase as comments
