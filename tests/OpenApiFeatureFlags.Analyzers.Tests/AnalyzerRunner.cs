using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenApiFeatureFlags.Analyzers.Tests;

/// <summary>
/// Runs an analyzer over a compilation built from source, the way the toolchain does. The
/// reference set is the test process's trusted platform list with the family assemblies and the
/// document engines filtered out; a scenario then adds exactly the family assemblies and engines it
/// wants, which is what makes the "package not referenced" cases provable.
/// </summary>
internal static class AnalyzerRunner
{
    /// <summary>Assemblies the runner always strips from the platform list.</summary>
    private static readonly string[] EngineFileNames =
    [
        "Microsoft.AspNetCore.OpenApi",
        "NSwag.AspNetCore",
        "NSwag.Generation.AspNetCore",
        "Swashbuckle.AspNetCore.SwaggerGen",
    ];

    internal static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        bool referenceCore = true,
        bool referenceNSwag = false,
        bool referenceAspNetCore = false,
        bool referenceBuiltInEngine = false,
        bool referenceNSwagEngine = false,
        params DiagnosticAnalyzer[] analyzers)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => !IsManagedByTheRunner(Path.GetFileNameWithoutExtension(path)))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        AddAssembly(references, "Microsoft.AspNetCore.OpenApi", referenceBuiltInEngine);
        // The NSwag adapter references NSwag.Generation.AspNetCore, not the NSwag.AspNetCore
        // middleware package, and that is the assembly a scenario can resolve here.
        AddAssembly(references, "NSwag.Generation.AspNetCore", referenceNSwagEngine);

        AddAssembly(references, "OpenApiFeatureFlags", referenceCore);
        // Every package of the family brings Abstractions transitively, and the attribute type
        // lives there, so it follows whichever adapter (or core) a scenario references.
        AddAssembly(references, "OpenApiFeatureFlags.Abstractions", referenceCore || referenceNSwag || referenceAspNetCore);
        AddAssembly(references, "OpenApiFeatureFlags.NSwag", referenceNSwag);
        AddAssembly(references, "OpenApiFeatureFlags.AspNetCore", referenceAspNetCore);

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

    private static bool IsManagedByTheRunner(string assemblyName) =>
        assemblyName.StartsWith("OpenApiFeatureFlags", StringComparison.OrdinalIgnoreCase) ||
        EngineFileNames.Contains(assemblyName, StringComparer.OrdinalIgnoreCase);

    private static void AddAssembly(List<MetadataReference> references, string name, bool include)
    {
        if (include)
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load(name).Location));
        }
    }
}
