using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Widgy.Core.Config;
using Widgy.Core.Interfaces;
using Widgy.Core.Plugin;
using Xunit;

namespace Widgy.Core.Tests;

public class PluginLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "widgy-plugin-tests-" + Guid.NewGuid().ToString("N"));
    private readonly WidgetPluginLoader _loader = new();

    public PluginLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        _loader.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private static byte[] BuildPlugin(string widgetId)
    {
        var source = $@"
using SkiaSharp;
using Widgy.Core;
using Widgy.Core.Attributes;
using Widgy.Core.Config;
using Widgy.Core.Enums;
using Widgy.Core.Rendering;
public class TestCfg : WidgetConfig {{ }}
[Widget(""T"", ""d"", Id = ""{widgetId}"")]
[WidgetSize(1, 1)]
[RefreshOnTick(1, TimeUnit.Seconds)]
public class TestWidget : Widget<TestCfg>
{{
    public override void Render(WidgetRenderContext context) {{ }}
}}";
        var refs = new List<MetadataReference>();
        var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (var p in tpa.Split(Path.PathSeparator))
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
        var path = Path.Combine(_dir, fileName);
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
        var path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        Assert.Contains("test.widget", _loader.GetRegisteredWidgetTypes());

        File.WriteAllBytes(path, BuildPlugin("test.widget.v2"));
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Reload_PicksUpReplacedPlugin()
    {
        var path = WritePlugin("test.widget");
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
        var path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        Assert.NotEmpty(_loader.GetRegisteredWidgetTypes());

        File.Delete(path);
        _loader.ReloadPlugins();

        Assert.Empty(_loader.GetRegisteredWidgetTypes());
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
