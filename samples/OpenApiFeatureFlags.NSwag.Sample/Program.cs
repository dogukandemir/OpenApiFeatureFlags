using OpenApiFeatureFlags;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Minimal APIs only reach the document when ApiExplorer knows about them: controllers register
// their own discovery, a minimal endpoint does not, and without this line it is silently absent
// from the document whatever its flags say. Measured, and recorded in docs/nswag.md.
builder.Services.AddEndpointsApiExplorer();

// One call: registers Microsoft.FeatureManagement and points OpenApiFeatureFlags at it.
// Flags come from appsettings.json -> "FeatureManagement".
builder.Services.AddOpenApiFeatureFlagsWithFeatureManagement(options =>
{
    options.Mode = DocumentMode.Remove;
});

// The two-argument overload hands over the application's IServiceProvider, which is how the
// processors receive the shared planner and the logger factory (docs/nswag.md).
builder.Services.AddOpenApiDocument((settings, services) =>
{
    settings.Title = "Sample API";
    settings.Version = "1.0";

    // Registered last: the processors act on what the document already contains.
    settings.AddOpenApiFeatureFlagProcessors(services);
});

var app = builder.Build();

app.MapControllers();

// Minimal APIs: the attribute arrives as endpoint metadata, and unlike the built-in engine this
// one can gate a minimal API's parameter too (docs/nswag.md).
app.MapGet("/api/quote", () => "quote")
    .WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout"));

app.MapGet("/api/search", ([OpenApiFeatureFlag("InternalSearch")] string? scope) => Results.Ok(scope));

app.UseOpenApi();
app.UseSwaggerUi();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
