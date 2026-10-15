using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenApiFeatureFlags.Analyzers;

/// <summary>
/// OAFF1002: an <c>[OpenApiFeatureFlag]</c> in a project that references a document engine but no
/// adapter package. The attribute is inert on its own, so without the adapter every gated element
/// stays in the document and nothing warns at document time. A project that references no engine
/// (a contracts library, say) is left alone: declaring the attribute there is legitimate and the
/// adapter lives in the host.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingAdapterAnalyzer : DiagnosticAnalyzer
{
    internal const string Id = DiagnosticIds.MissingAdapter;

    private static readonly (string DisplayName, string Adapter, string[] Assemblies)[] Engines =
    [
        ("Swashbuckle", KnownAssemblies.Swashbuckle, [KnownAssemblies.SwashbuckleEngine]),
        ("the built-in Microsoft.AspNetCore.OpenApi engine", KnownAssemblies.AspNetCore, [KnownAssemblies.BuiltInEngine]),
        ("NSwag", KnownAssemblies.NSwag, [KnownAssemblies.NSwagEngine, KnownAssemblies.NSwagGenerationEngine]),
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "No OpenApiFeatureFlags adapter package is referenced",
        messageFormat: "[OpenApiFeatureFlag] does nothing in this project: {0} is referenced, but no OpenApiFeatureFlags adapter package is; add {1} and register it next to the engine",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The attribute carries no behaviour: a document-engine adapter (OpenApiFeatureFlags.Swashbuckle, OpenApiFeatureFlags.AspNetCore or OpenApiFeatureFlags.NSwag) has to be referenced and registered for gated elements to leave the document.",
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
            if (KnownAssemblies.IsAdapterReferenced(start.Compilation))
            {
                return;
            }

            var engines = Engines
                .Where(engine => engine.Assemblies.Any(assembly => KnownAssemblies.IsReferenced(start.Compilation, assembly)))
                .ToList();

            if (engines.Count == 0)
            {
                return;
            }

            var attributeType = start.Compilation.GetTypeByMetadataName("OpenApiFeatureFlags.OpenApiFeatureFlagAttribute");
            if (attributeType is null)
            {
                return;
            }

            var engineNames = string.Join(" and ", engines.Select(engine => engine.DisplayName));
            var adapters = string.Join(" or ", engines.Select(engine => engine.Adapter).Distinct(StringComparer.Ordinal));

            start.RegisterSyntaxNodeAction(
                context => AnalyzeAttribute(context, attributeType, engineNames, adapters),
                SyntaxKind.Attribute);
        });
    }

    private static void AnalyzeAttribute(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol attributeType,
        string engineNames,
        string adapters)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol constructor &&
            SymbolEqualityComparer.Default.Equals(constructor.ContainingType, attributeType))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation(), engineNames, adapters));
        }
    }
}
