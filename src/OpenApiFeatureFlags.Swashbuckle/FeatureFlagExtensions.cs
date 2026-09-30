using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace OpenApiFeatureFlags.Swashbuckle;

/// <summary>
/// Extension names and helpers used to carry decisions from the filter that discovers a gated
/// element to the document filter that removes it.
/// </summary>
/// <remarks>
/// The marker is written into the document's own extension bag and stripped again before the
/// document is served, so a consumer never sees it. Object identity is used rather than property
/// names because serialisers rename properties (camel-casing, <c>[JsonPropertyName]</c>) and the
/// adapter must not reimplement those rules.
/// </remarks>
internal static class FeatureFlagExtensions
{
    /// <summary>Marks an element for removal. Never leaves the document.</summary>
    public const string RemoveMarker = "x-openapifeatureflags-remove";

    /// <summary>The public annotation emitted in <see cref="DocumentMode.Annotate"/> mode.</summary>
    public const string FlagAnnotation = "x-feature-flag";

    /// <summary>Marks an operation for removal.</summary>
    /// <param name="operation">The operation.</param>
    public static void MarkForRemoval(this OpenApiOperation operation)
    {
        var extensions = operation.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        extensions[RemoveMarker] = new JsonNodeExtension(JsonValue.Create(true));
    }

    /// <summary>Annotates an operation with the flags that gate it.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="flagNames">The gating flags.</param>
    public static void AnnotateWithFlags(this OpenApiOperation operation, IReadOnlyList<string> flagNames)
    {
        var extensions = operation.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        extensions[FlagAnnotation] = new JsonNodeExtension(ToJsonArray(flagNames));
    }

    /// <summary>Marks a schema for removal from its parent.</summary>
    /// <param name="schema">The schema.</param>
    /// <returns><see langword="true"/> when the schema could be marked.</returns>
    public static bool TryMarkForRemoval(this IOpenApiSchema schema) =>
        TrySetExtension(schema, RemoveMarker, new JsonNodeExtension(JsonValue.Create(true)));

    /// <summary>Determines whether an operation is marked for removal.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns><see langword="true"/> when it is marked.</returns>
    public static bool IsMarkedForRemoval(this OpenApiOperation operation) =>
        HasExtension(operation.Extensions, RemoveMarker);

    /// <summary>Determines whether a schema is marked for removal.</summary>
    /// <param name="schema">The schema.</param>
    /// <returns><see langword="true"/> when it is marked.</returns>
    public static bool IsMarkedForRemoval(this IOpenApiSchema? schema) =>
        HasExtension(ExtensionsOf(schema), RemoveMarker);

    /// <summary>Removes the internal marker from an operation.</summary>
    /// <param name="operation">The operation.</param>
    public static void StripMarker(this OpenApiOperation operation) =>
        operation.Extensions?.Remove(RemoveMarker);

    /// <summary>Removes the internal marker from a schema.</summary>
    /// <param name="schema">The schema.</param>
    public static void StripMarker(this IOpenApiSchema? schema) =>
        ExtensionsOf(schema)?.Remove(RemoveMarker);

    /// <summary>Builds a JSON array of flag names.</summary>
    /// <param name="flagNames">The flag names.</param>
    /// <returns>The JSON array.</returns>
    public static JsonArray ToJsonArray(IReadOnlyList<string> flagNames)
    {
        var array = new JsonArray();

        foreach (var flagName in flagNames)
        {
            array.Add(JsonValue.Create(flagName));
        }

        return array;
    }

    private static bool HasExtension(IDictionary<string, IOpenApiExtension>? extensions, string name) =>
        extensions is not null && extensions.ContainsKey(name);

    private static bool TrySetExtension(IOpenApiSchema schema, string name, IOpenApiExtension extension)
    {
        var extensions = GetOrCreateExtensions(schema);

        if (extensions is null)
        {
            return false;
        }

        extensions[name] = extension;
        return true;
    }

    private static IDictionary<string, IOpenApiExtension>? ExtensionsOf(IOpenApiSchema? schema) => schema switch
    {
        OpenApiSchema concrete => concrete.Extensions,
        OpenApiSchemaReference reference => reference.Extensions,
        _ => null,
    };

    private static IDictionary<string, IOpenApiExtension>? GetOrCreateExtensions(IOpenApiSchema schema)
    {
        switch (schema)
        {
            case OpenApiSchema concrete:
                return concrete.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            case OpenApiSchemaReference reference:
                return reference.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            default:
                // A $ref proxy we cannot attach to. Reported by the caller so the property stays
                // visible rather than being silently dropped.
                return null;
        }
    }
}
