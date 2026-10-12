using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenApiFeatureFlags.Analyzers.Tests;

/// <summary>
/// Runs an analyzer over a compilation built from source, the way the toolchain does. The
/// reference set is the test process's trusted platform list (so a minimal API compiles against
/// the real ASP.NET Core assemblies) with the family assemblies filtered out; a scenario then
/// adds exactly the family assemblies it wants, which is what makes the "package not referenced"
/// cases provable.
/// </summary>
internal static class AnalyzerRunner
{
    internal static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        bool referenceCore = true,
        bool referenceNSwag = false,
        bool referenceAspNetCore = false,
        params DiagnosticAnalyzer[] analyzers)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("OpenApiFeatureFlags", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        AddFamilyAssembly(references, "OpenApiFeatureFlags", referenceCore);
        // Every package of the family brings Abstractions transitively, and the attribute type
        // lives there, so it follows whichever adapter (or core) a scenario references.
        AddFamilyAssembly(references, "OpenApiFeatureFlags.Abstractions", referenceCore || referenceNSwag || referenceAspNetCore);
        AddFamilyAssembly(references, "OpenApiFeatureFlags.NSwag", referenceNSwag);
        AddFamilyAssembly(references, "OpenApiFeatureFlags.AspNetCore", referenceAspNetCore);

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest),
            path: "Test.cs");

        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return await compilation
            .WithAnalyzers(ImmutableArray.Create(analyzers))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static void AddFamilyAssembly(List<MetadataReference> references, string name, bool include)
    {
        if (include)
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load(name).Location));
        }
    }
}
