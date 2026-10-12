using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenApiFeatureFlags.Analyzers;

/// <summary>
/// OAFF5001: FastEndpoints does not carry attributes on an endpoint class or its members into
/// endpoint metadata, so an <c>[OpenApiFeatureFlag]</c> placed on one never gates anything
/// (measured against FastEndpoints 8.3.0; docs/fastendpoints.md). The supported route is
/// <c>Options(x => x.WithMetadata(new OpenApiFeatureFlagAttribute("..."))</c> in <c>Configure</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FastEndpointsAttributeAnalyzer : DiagnosticAnalyzer
{
    internal const string Id = DiagnosticIds.FastEndpointsAttribute;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Attribute on a FastEndpoints endpoint does not gate it",
        messageFormat: "FastEndpoints does not carry attributes on the endpoint (class or members) into endpoint metadata, so this [OpenApiFeatureFlag] never reaches the document; gate the endpoint with Options(x => x.WithMetadata(new OpenApiFeatureFlagAttribute(\"...\"))) in Configure",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The adapters read gating from endpoint metadata for non-controller endpoints, and FastEndpoints fills that metadata from WithMetadata calls rather than from attributes on the endpoint type (docs/fastendpoints.md).",
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
            var attributeType = start.Compilation.GetTypeByMetadataName("OpenApiFeatureFlags.OpenApiFeatureFlagAttribute");
            if (attributeType is null)
            {
                return;
            }

            start.RegisterSymbolAction(
                context => AnalyzeSymbol(context, attributeType),
                SymbolKind.NamedType,
                SymbolKind.Method,
                SymbolKind.Property);
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol attributeType)
    {
        if (context.Symbol is INamedTypeSymbol type)
        {
            if (IsFastEndpointsEndpoint(type))
            {
                ReportAttribute(context, attributeType, type.GetAttributes());
            }

            return;
        }

        var containingType = context.Symbol.ContainingType;
        if (containingType is not null && IsFastEndpointsEndpoint(containingType))
        {
            ReportAttribute(context, attributeType, context.Symbol.GetAttributes());
        }
    }

    private static void ReportAttribute(
        SymbolAnalysisContext context,
        INamedTypeSymbol attributeType,
        ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? context.Symbol.Locations.FirstOrDefault();

            if (location is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        }
    }

    private static bool IsFastEndpointsEndpoint(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var definition = current.OriginalDefinition;

            if (definition.ContainingNamespace?.ToDisplayString() == "FastEndpoints" &&
                definition.Name.StartsWith("Endpoint", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
