using FastEndpoints;
using FastEndpoints.OpenApi;
using OpenApiFeatureFlags;

var bld = WebApplication.CreateBuilder(args);

// One call: registers Microsoft.FeatureManagement and points OpenApiFeatureFlags at it.
// Flags come from appsettings.json -> "FeatureManagement".
bld.Services.AddOpenApiFeatureFlagsWithFeatureManagement(options =>
{
    options.Mode = DocumentMode.Remove;
});

bld.Services.AddFastEndpoints()
    .OpenApiDocument(o =>
    {
        o.Title = "Orders API";
        o.Version = "1.0";

        // Hand the built-in engine's options to this library. FastEndpoints runs these
        // transformers alongside its own.
        o.ConfigureOpenApi = options => options.AddOpenApiFeatureFlagTransformers();
    });

var app = bld.Build();

app.UseFastEndpoints();
app.MapOpenApi();   // serves /openapi/v1.json

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.Run();
