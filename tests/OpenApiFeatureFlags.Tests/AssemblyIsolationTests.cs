using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.Tests;

/// <summary>
/// Guards decision D10 / invariant 1: the contract assembly must stay dependency-free so that
/// contract projects can carry the attribute without being forced onto Swashbuckle or a
/// feature-flag library. These tests exist so a future PR cannot regress it.
/// </summary>
public sealed class AssemblyIsolationTests
{
    private static readonly string[] AllowedReferences =
    [
        "netstandard",
        "mscorlib",
        "System",
    ];

    [Fact]
    public void AbstractionsReferencesNothingButTheFramework()
    {
        var offenders = typeof(OpenApiFeatureFlagAttribute).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Where(name => !IsFrameworkAssembly(name!))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        offenders.ShouldBeEmpty(
            $"OpenApiFeatureFlags.Abstractions must stay dependency-free (D10) but references: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void EveryPublicTypeLivesInTheSingleOpenApiFeatureFlagsNamespace()
    {
        var namespaces = typeof(OpenApiFeatureFlagAttribute).Assembly
            .GetExportedTypes()
            .Select(type => type.Namespace)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // Consumers write `using OpenApiFeatureFlags;` and never see the package name.
        namespaces.ShouldBe(["OpenApiFeatureFlags"]);
    }

    private static bool IsFrameworkAssembly(string name) =>
        AllowedReferences.Contains(name, StringComparer.Ordinal)
        || name.StartsWith("System.", StringComparison.Ordinal);
}
