using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

// The test namespace contains "Swashbuckle", which shadows the real one, so the global:: alias is needed.
using ISwaggerProvider = global::Swashbuckle.AspNetCore.Swagger.ISwaggerProvider;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>Builds a real Swashbuckle document through a real service provider.</summary>
internal static class TestSwagger
{
    public static OpenApiDocument Build(
        IFeatureFlagSource flagSource,
        DocumentMode mode = DocumentMode.Remove,
        bool canaryEnabled = true,
        bool useFeatureFlagFilters = true,
        Action<SwaggerGenOptions>? configureSwagger = null,
        Type[]? controllers = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment());

        var mvc = services.AddControllers();
        mvc.ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Add(new AssemblyPart(typeof(TestSwagger).Assembly));

            // Restricting the document to a chosen controller set is what makes the
            // byte-identical regression test possible: it needs a document with no gating at all.
            if (controllers is { Length: > 0 })
            {
                manager.FeatureProviders.Add(new SelectedControllersFeatureProvider(controllers));
            }
        });

        services.AddOpenApiFeatureFlags(flagSource, options =>
        {
            options.Mode = mode;
            options.CanaryEnabled = canaryEnabled;
        });

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Test API", Version = "1.0" });
            configureSwagger?.Invoke(options);

            // Registered last on purpose, so a consumer processor configured above runs between
            // operation pruning and tag pruning, exactly as the documentation requires.
            if (useFeatureFlagFilters)
            {
                options.AddOpenApiFeatureFlagFilters();
            }
        });

        using var provider = services.BuildServiceProvider();
        var swaggerProvider = provider.GetRequiredService<ISwaggerProvider>();
        return swaggerProvider.GetSwagger("v1");
    }

    public static string ToJson(OpenApiDocument document)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        document.SerializeAsV3(new OpenApiJsonWriter(writer));
        return writer.ToString();
    }

    /// <summary>Every path in the document, in a stable order.</summary>
    public static string[] Paths(OpenApiDocument document) =>
        document.Paths is null
            ? []
            : [.. document.Paths.Keys.OrderBy(key => key, StringComparer.Ordinal)];

    public static bool HasOperation(OpenApiDocument document, string path, HttpMethod method) =>
        document.Paths is not null
        && document.Paths.TryGetValue(path, out var pathItem)
        && pathItem is OpenApiPathItem concrete
        && concrete.Operations is not null
        && concrete.Operations.ContainsKey(method);

    public static bool HasProperty(OpenApiDocument document, string schemaName, string propertyName) =>
        document.Components?.Schemas is { } schemas
        && schemas.TryGetValue(schemaName, out var schema)
        && schema is OpenApiSchema concrete
        && concrete.Properties?.ContainsKey(propertyName) == true;

    public static bool HasComponent(OpenApiDocument document, string schemaName) =>
        document.Components?.Schemas?.ContainsKey(schemaName) == true;

    public static OpenApiSchema Schema(OpenApiDocument document, string schemaName) =>
        (OpenApiSchema)document.Components!.Schemas![schemaName];

    public static string[] RequiredProperties(OpenApiDocument document, string schemaName)
    {
        var required = Schema(document, schemaName).Required;
        return required is null ? [] : [.. required];
    }

    public static string[] TagNames(OpenApiDocument document)
    {
        var tags = document.Tags;
        return tags is null ? [] : [.. tags.Select(tag => tag.Name ?? string.Empty)];
    }

    public static string[] ParameterNames(OpenApiDocument document, string path, HttpMethod method) =>
        [.. OperationAt(document, path, method).Parameters?.Select(parameter => parameter.Name ?? string.Empty) ?? []];

    public static OpenApiOperation OperationAt(OpenApiDocument document, string path, HttpMethod method)
    {
        var pathItem = (OpenApiPathItem)document.Paths![path];
        return pathItem.Operations![method];
    }
}
