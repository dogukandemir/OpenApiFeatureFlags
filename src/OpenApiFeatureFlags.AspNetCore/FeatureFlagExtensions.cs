using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace OpenApiFeatureFlags.AspNetCore;

/// <summary>
/// Extension names and helpers used to carry decisions from the transformer that discovers a gated
/// element to the one that removes it.
/// </summary>
/// <remarks>
/// <para>
/// The marker is written into the document's own extension bag and stripped again before the document
/// is served, so a consumer never sees it.
/// </para>
/// <para>
/// An operation transformer is handed one operation and no way to reach its path item, so it cannot
/// remove anything; it marks. The document transformer runs last, has the whole document, and does the
/// removing.
/// </para>
/// </remarks>
internal static class FeatureFlagExtensions
{
    /// <summary>Marks an element for removal. Never leaves the document.</summary>
    public const string RemoveMarker = "x-openapifeatureflags-remove";

    /// <summary>The public annotation emitted in <see cref="DocumentMode.Annotate"/> mode.</summary>
    public const string FlagAnnotation = "x-feature-flag";

    /// <summary>Marks an operation for removal.</summary>
    public static void MarkForRemoval(this OpenApiOperation operation)
    {
        var extensions = operation.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        extensions[RemoveMarker] = new JsonNodeExtension(JsonValue.Create(true));
    }

    /// <summary>Annotates an operation with the flags that gate it.</summary>
    public static void AnnotateWithFlags(this OpenApiOperation operation, IReadOnlyList<string> flagNames)
    {
        var extensions = operation.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        extensions[FlagAnnotation] = new JsonNodeExtension(ToJsonArray(flagNames));
    }

    /// <summary>Determines whether an operation is marked for removal.</summary>
    public static bool IsMarkedForRemoval(this OpenApiOperation operation) =>
        operation.Extensions?.ContainsKey(RemoveMarker) == true;

    /// <summary>Removes the internal marker from an operation.</summary>
    public static void StripMarker(this OpenApiOperation operation) =>
        operation.Extensions?.Remove(RemoveMarker);

    /// <summary>Builds a JSON array of flag names.</summary>
    public static JsonArray ToJsonArray(IReadOnlyList<string> flagNames)
    {
        var array = new JsonArray();

        foreach (var flagName in flagNames)
        {
            array.Add(JsonValue.Create(flagName));
        }

        return array;
    }
}
