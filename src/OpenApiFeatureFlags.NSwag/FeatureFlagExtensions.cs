using global::NSwag;

namespace OpenApiFeatureFlags.NSwag;

/// <summary>
/// Helpers used to carry this library's public annotation into the generated document.
/// </summary>
/// <remarks>
/// Unlike the Swashbuckle and ASP.NET Core adapters there is no removal marker here: NSwag's
/// operation processor can keep an operation out of the document by returning <see langword="false"/>,
/// so only the public annotation needs a shared home.
/// </remarks>
internal static class FeatureFlagExtensions
{
    /// <summary>The public annotation emitted in <see cref="DocumentMode.Annotate"/> mode.</summary>
    public const string FlagAnnotation = "x-feature-flag";

    /// <summary>Annotates an operation with the flags that gate it.</summary>
    public static void AnnotateWithFlags(this OpenApiOperation operation, IReadOnlyList<string> flagNames)
    {
        operation.ExtensionData ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        operation.ExtensionData[FlagAnnotation] = flagNames.ToArray();
    }
}
