using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Widgy.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WidgetAnalyzer : DiagnosticAnalyzer
    {
        public const string IdWidget = "WIDGY001";
        public const string IdWidgetSize = "WIDGY002";
        public const string IdNoRefresh = "WIDGY003";
        public const string IdMultipleRefresh = "WIDGY004";
        public const string IdInvalidSize = "WIDGY005";

        private static readonly DiagnosticDescriptor RuleWidget = new(
            IdWidget, "Missing [Widget]", "Widget class must have [Widget] attribute", "Usage", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor RuleWidgetSize = new(
            IdWidgetSize, "Missing [WidgetSize]", "Widget class must have at least one [WidgetSize] attribute", "Usage", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor RuleNoRefresh = new(
            IdNoRefresh, "Missing refresh strategy", "Widget must have exactly one refresh strategy attribute", "Usage", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor RuleMultipleRefresh = new(
            IdMultipleRefresh, "Conflicting refresh strategies", "Widget must have at most one refresh strategy attribute", "Usage", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor RuleInvalidSize = new(
            IdInvalidSize, "Invalid widget size", "Width must be 1-4, Height must be >= 1", "Usage", DiagnosticSeverity.Error, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            RuleWidget, RuleWidgetSize, RuleNoRefresh, RuleMultipleRefresh, RuleInvalidSize);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.ClassDeclaration);
        }

        private void AnalyzeType(SyntaxNodeAnalysisContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var model = context.SemanticModel;

            var symbol = model.GetDeclaredSymbol(classDecl);
            if (symbol == null) return;

            var ns = symbol.ContainingNamespace;
            if (ns.IsGlobalNamespace) return;

            var baseType = symbol.BaseType;
            if (baseType == null || baseType.Name != "Widget") return;

            var allAttrs = new List<AttributeSyntax>();
            foreach (var al in classDecl.AttributeLists)
            {
                foreach (var a in al.Attributes)
                {
                    allAttrs.Add(a);
                }
            }

            var hasWidget = false;
            var hasWidgetSize = false;
            var refreshCount = 0;
            var sizeAttrs = new List<AttributeSyntax>();

            foreach (var attr in allAttrs)
            {
                var name = attr.Name.ToString();
                if (name == "Widget") hasWidget = true;
                if (name == "WidgetSize")
                {
                    hasWidgetSize = true;
                    sizeAttrs.Add(attr);
                }
                if (name == "RefreshOnTick" || name == "RefreshAdaptive" || name == "RefreshOnEvent")
                {
                    refreshCount++;
                }
            }

            if (!hasWidget)
                context.ReportDiagnostic(Diagnostic.Create(RuleWidget, classDecl.GetLocation()));

            if (!hasWidgetSize)
                context.ReportDiagnostic(Diagnostic.Create(RuleWidgetSize, classDecl.GetLocation()));

            if (refreshCount == 0)
                context.ReportDiagnostic(Diagnostic.Create(RuleNoRefresh, classDecl.GetLocation()));

            if (refreshCount > 1)
                context.ReportDiagnostic(Diagnostic.Create(RuleMultipleRefresh, classDecl.GetLocation()));

            foreach (var ws in sizeAttrs)
            {
                var args = ws.ArgumentList;
                if (args == null) continue;

                var argList = new List<Microsoft.CodeAnalysis.CSharp.Syntax.AttributeArgumentSyntax>();
                foreach (var arg in args.Arguments)
                {
                    argList.Add(arg);
                }

                if (argList.Count >= 2)
                {
                    var w = ExtractLiteralInt(argList[0]);
                    var h = ExtractLiteralInt(argList[1]);
                    if (w != -1 && h != -1 && (w < 1 || w > 4 || h < 1))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(RuleInvalidSize, ws.GetLocation()));
                    }
                }
            }
        }

        private static int ExtractLiteralInt(Microsoft.CodeAnalysis.CSharp.Syntax.AttributeArgumentSyntax arg)
        {
            var expr = arg.Expression as Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax;
            if (expr == null) return -1;
            if (!expr.Token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NumericLiteralToken)) return -1;
            if (expr.Token.Value is int v) return v;
            if (expr.Token.Value is long vl) return (int)vl;
            return -1;
        }
    }
}
