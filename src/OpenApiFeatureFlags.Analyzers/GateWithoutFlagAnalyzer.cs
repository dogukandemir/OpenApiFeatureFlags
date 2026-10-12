using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenApiFeatureFlags.Analyzers;

/// <summary>
/// OAFF1001: a <c>&lt;gate&gt;</c> fragment with no flag attribute hides its content in every
/// flag state (fail closed) on the engines that read documentation comments as written. Under
/// NSwag the same fragment is dropped instead of hidden, and OAFF3001 speaks for that engine,
/// so this rule stays silent when the NSwag adapter is referenced.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GateWithoutFlagAnalyzer : DiagnosticAnalyzer
{
    internal const string Id = DiagnosticIds.GateWithoutFlag;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Gate fragment without a flag attribute",
        messageFormat: "This <gate> fragment has no flag attribute, so its content is hidden in every flag state; add flag=\"...\" or remove the fragment if this was accidental",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A <gate> element without a flag attribute is treated as fail-closed by the document engines and the content is never documented. This is usually a missing flag.",
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
            if (!KnownAssemblies.IsFamilyReferenced(start.Compilation) ||
                KnownAssemblies.IsReferenced(start.Compilation, KnownAssemblies.NSwag))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeGateElement, SyntaxKind.XmlElement);
        });
    }

    private static void AnalyzeGateElement(SyntaxNodeAnalysisContext context)
    {
        var element = (XmlElementSyntax)context.Node;

        if (GateSyntax.IsGateElement(element) && !GateSyntax.HasFlagAttribute(element))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, element.StartTag.GetLocation()));
        }
    }
}
