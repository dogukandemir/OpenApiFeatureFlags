using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenApiFeatureFlags.Analyzers;

/// <summary>
/// OAFF3001: NSwag's documentation reader flattens element markup in XML documentation comments,
/// so a literal <c>&lt;gate&gt;</c> tag is dropped and the fragment is not gated: its content
/// stays visible in every flag state (fail open, silent). The escaped form survives the reader as
/// text and resolves as a gate. The rule fires only when the NSwag adapter is referenced.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NSwagEscapeGateAnalyzer : DiagnosticAnalyzer
{
    internal const string Id = DiagnosticIds.NswagEscapeGate;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Escape gate fragments in XML documentation comments (NSwag)",
        messageFormat: "NSwag's documentation reader drops the literal <gate> tag, so this fragment is not gated and its content stays visible in every flag state; write it escaped: &lt;gate flag=\"...\"&gt;...&lt;/gate&gt;",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "NSwag reads documentation comments into flattened text, so XML elements inside them never reach the gate resolver. The escaped form is decoded to the same text the other engines produce, which resolves correctly (docs/nswag.md).",
        helpLinkUri: DiagnosticIds.HelpLink(Id));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            if (!KnownAssemblies.IsReferenced(start.Compilation, KnownAssemblies.NSwag))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeGateElement, SyntaxKind.XmlElement);
        });
    }

    private static void AnalyzeGateElement(SyntaxNodeAnalysisContext context)
    {
        var element = (XmlElementSyntax)context.Node;

        if (GateSyntax.IsGateElement(element))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, element.StartTag.GetLocation()));
        }
    }
}
