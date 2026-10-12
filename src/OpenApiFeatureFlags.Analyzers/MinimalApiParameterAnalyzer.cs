using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenApiFeatureFlags.Analyzers;

/// <summary>
/// OAFF2001: the built-in <c>Microsoft.AspNetCore.OpenApi</c> engine cannot gate a parameter of a
/// minimal API delegate, so an <c>[OpenApiFeatureFlag]</c> written on a lambda parameter is
/// silently ignored. The rule fires only when the built-in engine's adapter is referenced and the
/// NSwag adapter is not, because under NSwag the same attribute works.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MinimalApiParameterAnalyzer : DiagnosticAnalyzer
{
    internal const string Id = DiagnosticIds.MinimalApiParameter;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Minimal API parameters cannot be gated by the built-in engine",
        messageFormat: "The built-in Microsoft.AspNetCore.OpenApi engine ignores [OpenApiFeatureFlag] on minimal API parameters; gate the operation instead with .WithMetadata(new OpenApiFeatureFlagAttribute(\"...\")), or use the NSwag adapter, which can gate parameters",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "OpenApiFeatureFlags.AspNetCore gates operations, parameters and schema properties of controller-based APIs; a minimal API's parameters have no removal path in that engine, so an attribute there never takes effect (docs/aspnetcore.md).",
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
            if (!KnownAssemblies.IsReferenced(start.Compilation, KnownAssemblies.AspNetCore) ||
                KnownAssemblies.IsReferenced(start.Compilation, KnownAssemblies.NSwag))
            {
                return;
            }

            var attributeType = start.Compilation.GetTypeByMetadataName("OpenApiFeatureFlags.OpenApiFeatureFlagAttribute");
            if (attributeType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                context => AnalyzeParameter(context, attributeType),
                SyntaxKind.Parameter);
        });
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context, INamedTypeSymbol attributeType)
    {
        var parameter = (ParameterSyntax)context.Node;

        if (!IsLambdaParameter(parameter))
        {
            return;
        }

        foreach (var attribute in parameter.AttributeLists.SelectMany(list => list.Attributes))
        {
            if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol constructor &&
                SymbolEqualityComparer.Default.Equals(constructor.ContainingType, attributeType))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation()));
            }
        }
    }

    private static bool IsLambdaParameter(ParameterSyntax parameter) =>
        parameter.Parent switch
        {
            LambdaExpressionSyntax => true,
            ParameterListSyntax { Parent: LambdaExpressionSyntax } => true,
            _ => false,
        };
}
