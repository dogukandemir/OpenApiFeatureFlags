using OpenApiFeatureFlags;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// One call: registers Microsoft.FeatureManagement and points OpenApiFeatureFlags at it.
// Flags come from appsettings.json -> "FeatureManagement".
builder.Services.AddOpenApiFeatureFlagsWithFeatureManagement(options =>
{
    options.Mode = DocumentMode.Remove;
});

builder.Services.AddOpenApi(options =>
{
    options.AddOpenApiFeatureFlagTransformers();
});

var app = builder.Build();

app.MapControllers();

// Minimal APIs arrive through endpoint metadata. A minimal API's parameter cannot be gated on this
// engine, and docs/aspnetcore.md records why, so this example gates the operation only.
app.MapGet("/api/quote", () => "quote")
    .WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout"));

app.MapOpenApi();   // serves /openapi/v1.json

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.Run();
