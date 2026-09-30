using Microsoft.OpenApi;
using OpenApiFeatureFlags;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// One call: registers Microsoft.FeatureManagement and points OpenApiFeatureFlags at it.
// Flags come from appsettings.json -> "FeatureManagement".
builder.Services.AddOpenApiFeatureFlagsWithFeatureManagement(options =>
{
    options.Mode = DocumentMode.Remove;
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Sample API", Version = "1.0" });

    // Registered last: our operation pruning has to run after any processor that rebuilds Paths,
    // and our tag pruning after any processor that rebuilds Tags.
    options.AddOpenApiFeatureFlagFilters();
});

var app = builder.Build();

app.MapControllers();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
