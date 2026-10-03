// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UrDeck.Engine.Plugin;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class PluginLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-plugin-tests-" + Guid.NewGuid().ToString("N"));
    private readonly WidgetPluginLoader _loader = new();

    public PluginLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        _loader.Dispose();
        try
        { Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    private static byte[] BuildPlugin(string widgetId)
    {
        string source = $@"
using SkiaSharp;
using UrDeck.Sdk;
public class TestCfg : WidgetConfig {{ }}
[Widget(""T"", ""d"", Id = ""{widgetId}"")]
[WidgetSize(1, 1)]
[RefreshOnTick(1, TimeUnit.Seconds)]
public class TestWidget : Widget<TestCfg>
{{
    public override void Render(WidgetRenderContext context) {{ }}
}}";
        var refs = new List<MetadataReference>();
        string tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (string p in tpa.Split(Path.PathSeparator))
            refs.Add(MetadataReference.CreateFromFile(p));

        var compilation = CSharpCompilation.Create("TestPlugin_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(source) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return ms.ToArray();
    }

    private string WritePlugin(string widgetId, string fileName = "TestPlugin.dll")
    {
        string path = Path.Combine(_dir, fileName);
        File.WriteAllBytes(path, BuildPlugin(widgetId));
        return path;
    }

    [Fact]
    public void LoadsPlugin_InOwnContext_WithSharedContractTypes()
    {
        WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);

        Assert.Contains("test.widget", _loader.GetRegisteredWidgetTypes());
        var widget = _loader.CreateWidget(new WidgetConfig { WidgetTypeId = "test.widget" });

        Assert.NotNull(widget);
        var alc = AssemblyLoadContext.GetLoadContext(widget!.GetType().Assembly);
        Assert.NotNull(alc);
        Assert.NotSame(AssemblyLoadContext.Default, alc);
        Assert.True(alc!.IsCollectible);
        Assert.IsAssignableFrom<IWidget>(widget);
    }

    [Fact]
    public void OriginalFile_IsNotLocked_WhileLoaded()
    {
        string path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        Assert.Contains("test.widget", _loader.GetRegisteredWidgetTypes());

        File.WriteAllBytes(path, BuildPlugin("test.widget.v2"));
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Reload_PicksUpReplacedPlugin()
    {
        string path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        Assert.Contains("test.widget", _loader.GetRegisteredWidgetTypes());

        File.WriteAllBytes(path, BuildPlugin("test.widget.v2"));
        _loader.ReloadPlugins();

        var types = _loader.GetRegisteredWidgetTypes();
        Assert.Contains("test.widget.v2", types);
        Assert.DoesNotContain("test.widget", types);
        Assert.NotNull(_loader.CreateWidget(new WidgetConfig { WidgetTypeId = "test.widget.v2" }));
    }

    [Fact]
    public void Reload_AfterDelete_LeavesRegistryEmpty()
    {
        string path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        Assert.NotEmpty(_loader.GetRegisteredWidgetTypes());

        File.Delete(path);
        _loader.ReloadPlugins();

        Assert.Empty(_loader.GetRegisteredWidgetTypes());
    }

    [Fact]
    public void Reload_AllowsOldPluginContextToBeCollected()
    {
        string path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        var contextRef = CreateAndRenderWidget();

        File.Delete(path);
        _loader.ReloadPlugins();

        // The loader nudges System.Text.Json's accessor cache ~1.5s after unload; allow for that.
        for (int i = 0; i < 50 && contextRef.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }
        Assert.False(contextRef.IsAlive, "Unloaded plugin context is still referenced (leak on hot-reload).");
    }

    // Separate non-inlined method so no locals keep the widget or its context alive in the caller.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private WeakReference CreateAndRenderWidget()
    {
        var config = new WidgetConfig { WidgetTypeId = "test.widget", Width = 1, Height = 1 };
        var widget = _loader.CreateWidget(config)!;
        using var bitmap = UrDeck.Engine.Rendering.PageRenderer.RenderToBitmap(
            100, 100, new[] { (config, widget) }, new UrDeck.Engine.Themes.ThemeStore("").Load(UrDeck.Engine.Themes.ThemeStore.DefaultName), DateTime.Now);
        return new WeakReference(AssemblyLoadContext.GetLoadContext(widget.GetType().Assembly));
    }

    [Fact]
    public void GarbageDll_IsSkipped_OthersStillLoad()
    {
        File.WriteAllText(Path.Combine(_dir, "bad.dll"), "this is not a .NET assembly");
        WritePlugin("test.widget");

        _loader.ScanAndLoadPlugins(_dir);

        Assert.Equal(new[] { "test.widget" }, _loader.GetRegisteredWidgetTypes());
    }
}
