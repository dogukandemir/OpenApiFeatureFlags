using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.OpenFeature.Tests;

public sealed class OpenFeatureWiringTests
{
    [Fact]
    public void OneCallRegistersEverything()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOpenApiFeatureFlagsWithOpenFeature(options => options.Mode = DocumentMode.Annotate);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDocumentVisibilityPlanner>().Mode.ShouldBe(DocumentMode.Annotate);
        provider.GetRequiredService<IFeatureFlagSource>().ShouldBeOfType<OpenFeatureFlagSource>();
    }

    [Fact]
    public void FlagSourceCanBeAddedAfterTheCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOpenApiFeatureFlags().AddOpenFeatureFlagSource();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IFeatureFlagSource>().ShouldBeOfType<OpenFeatureFlagSource>();
    }

    [Fact]
    public void DoesNotReplaceAFlagSourceTheApplicationRegisteredItself()
    {
        var custom = new CustomFlagSource();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFeatureFlagSource>(custom);

        services.AddOpenApiFeatureFlagsWithOpenFeature();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IFeatureFlagSource>().ShouldBeSameAs(custom);
    }

    [Fact]
    public void EndToEndAnUnconfiguredProviderFailsClosedWithoutTrippingTheCanary()
    {
        // The global Api singleton starts on a provider that answers every flag with the caller's
        // default. That is a *successful* read, so the document is published with the gated surface
        // hidden and the canary stays quiet - an unreachable store is the only thing that aborts.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenApiFeatureFlagsWithOpenFeature();

        using var provider = services.BuildServiceProvider();
        var planner = provider.GetRequiredService<IDocumentVisibilityPlanner>();

        planner.IsHidden(["anything"]).ShouldBeTrue();
        Should.NotThrow(() => planner.CompleteDocument());
    }

    [Fact]
    public void RejectsAMissingServiceCollection()
    {
        Should.Throw<ArgumentNullException>(() =>
            OpenApiFeatureFlagsOpenFeatureExtensions.AddOpenApiFeatureFlagsWithOpenFeature(null!));
        Should.Throw<ArgumentNullException>(() =>
            OpenApiFeatureFlagsOpenFeatureExtensions.AddOpenFeatureFlagSource(null!));
    }

    private sealed class CustomFlagSource : IFeatureFlagSource
    {
        public bool IsEnabled(string flagName) => true;
    }
}
