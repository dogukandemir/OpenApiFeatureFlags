using Microsoft.CodeAnalysis;

namespace OpenApiFeatureFlags.Analyzers;

/// <summary>The assembly names the rules key off, exactly as they ship on NuGet.</summary>
internal static class KnownAssemblies
{
    internal const string Core = "OpenApiFeatureFlags";
    internal const string AspNetCore = "OpenApiFeatureFlags.AspNetCore";
    internal const string NSwag = "OpenApiFeatureFlags.NSwag";

    internal static bool IsReferenced(Compilation compilation, string assemblyName) =>
        compilation.ReferencedAssemblyNames.Any(
            identity => string.Equals(identity.Name, assemblyName, StringComparison.Ordinal));

    /// <summary>True when any package of the family is referenced (core or an adapter).</summary>
    internal static bool IsFamilyReferenced(Compilation compilation) =>
        compilation.ReferencedAssemblyNames.Any(identity =>
            string.Equals(identity.Name, Core, StringComparison.Ordinal) ||
            identity.Name.StartsWith(Core + ".", StringComparison.Ordinal));
}
