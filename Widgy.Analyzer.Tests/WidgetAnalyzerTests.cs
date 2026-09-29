using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Widgy.Analyzer;
using Xunit;

namespace Widgy.Analyzer.Tests;

public class WidgetAnalyzerTests
{
    private const string Usings = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Widgy.Core;
using Widgy.Core.Attributes;
using Widgy.Core.Config;
using Widgy.Core.Enums;
using Widgy.Core.Rendering;
public class Cfg : WidgetConfig { }
";

    private const string Body = @"
{
    public override string Name => ""x"";
    public override string Description => ""x"";
    public override string Category => ""x"";
    public override System.Drawing.Size[] SupportedSizes => new System.Drawing.Size[0];
    public override Cfg DefaultConfig => new Cfg();
    public override Type ConfigType => typeof(Cfg);
    public override Task RenderAsync(SKCanvas canvas, WidgetRenderContext context, CancellationToken token) => Task.CompletedTask;
}";

    private const string Widget = "[Widget(\"a\", \"b\")]";
    private const string Size = "[WidgetSize(2, 1)]";
    private const string Tick = "[RefreshOnTick(1, TimeUnit.Seconds)]";

    private static string[] Run(string attrs, string decl = "public class W : Widget<Cfg>", string? extra = null)
    {
        var source = Usings + (extra ?? "") + attrs + "\n" + decl + Body;
        var tree = CSharpSyntaxTree.ParseText(source);

        var refs = new List<MetadataReference>();
        var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (var p in tpa.Split(Path.PathSeparator))
            refs.Add(MetadataReference.CreateFromFile(p));
        refs.Add(MetadataReference.CreateFromFile(typeof(Widgy.Core.Attributes.WidgetAttribute).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(typeof(SkiaSharp.SKCanvas).Assembly.Location));

        var compilation = CSharpCompilation.Create("t", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Sanity: no compile errors in the test source itself.
        var compileErrors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
        Assert.Empty(compileErrors);

        var diags = compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new WidgetAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        Assert.All(diags, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        return diags.Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void ValidWidget_NoDiagnostics() =>
        Assert.Empty(Run(Widget + Size + Tick));

    [Fact]
    public void MissingWidget_Widgy001() =>
        Assert.Equal(new[] { "WIDGY001" }, Run(Size + Tick));

    [Fact]
    public void MissingSize_Widgy002() =>
        Assert.Equal(new[] { "WIDGY002" }, Run(Widget + Tick));

    [Fact]
    public void MissingRefresh_Widgy003() =>
        Assert.Equal(new[] { "WIDGY003" }, Run(Widget + Size));

    [Fact]
    public void MultipleRefresh_Widgy004() =>
        Assert.Equal(new[] { "WIDGY004" }, Run(Widget + Size + Tick + "[RefreshOnEvent(\"e\")]"));

    [Theory]
    [InlineData("[WidgetSize(5, 1)]")]
    [InlineData("[WidgetSize(0, 1)]")]
    [InlineData("[WidgetSize(2, 0)]")]
    public void InvalidSize_Widgy005(string size) =>
        Assert.Equal(new[] { "WIDGY005" }, Run(Widget + size + Tick));

    [Fact]
    public void MultipleValidSizes_NoDiagnostics() =>
        Assert.Empty(Run(Widget + Size + "[WidgetSize(4, 3)]" + "[RefreshAdaptive(100, 1000)]"));

    [Fact]
    public void AbstractWidget_NotFlagged() =>
        Assert.Empty(Run("", "public abstract class W : Widget<Cfg>"));

    [Fact]
    public void IndirectSubclass_IsAnalyzed() =>
        Assert.Equal(new[] { "WIDGY001", "WIDGY002", "WIDGY003" },
            Run("", "public class W : Base", "public abstract class Base : Widget<Cfg>" + Body + "\n"));

    [Fact]
    public void NonWidgetClass_NotFlagged() =>
        Assert.Empty(Run(Widget + Size + Tick, "public class W : Widget<Cfg>", "public class Plain { }"));
}
