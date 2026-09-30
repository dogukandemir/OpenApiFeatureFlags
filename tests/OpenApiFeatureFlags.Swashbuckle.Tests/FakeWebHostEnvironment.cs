using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// Swashbuckle's <c>ConfigureSwaggerGeneratorOptions</c> requires <see cref="IWebHostEnvironment"/>.
/// The tests build a bare service provider rather than a web host, so this stub supplies it.
/// </summary>
internal sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "OpenApiFeatureFlags.Swashbuckle.Tests";

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public string WebRootPath { get; set; } = Path.GetTempPath();

    public string EnvironmentName { get; set; } = "Development";

    public string ContentRootPath { get; set; } = Path.GetTempPath();

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
