using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;
using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.FeatureManagement.Tests;

/// <summary>
/// One extension-method call wires the whole thing up for a Microsoft.FeatureManagement consumer.
/// </summary>
public sealed class FeatureManagementWiringTests
{
    [Fact]
    public void OneCallRegistersEverything()
    {
        var services = NewServices();

        services.AddOpenApiFeatureFlagsWithFeatureManagement(options => options.Mode = DocumentMode.Annotate);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDocumentVisibilityPlanner>().Mode.ShouldBe(DocumentMode.Annotate);
        provider.GetRequiredService<IFeatureManager>().ShouldNotBeNull();
        provider.GetRequiredService<IFeatureFlagSource>().ShouldBeOfType<FeatureManagerFlagSource>();
    }

    [Fact]
    public void FlagSourceCanBeAddedAfterTheCoreServices()
    {
        var services = NewServices();

        services.AddOpenApiFeatureFlags().AddFeatureManagementFlagSource();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IFeatureFlagSource>().ShouldBeOfType<FeatureManagerFlagSource>();
    }

    [Fact]
    public void DoesNotReplaceAFlagSourceTheApplicationRegisteredItself()
    {
        var custom = new CustomFlagSource();

        var services = NewServices();
        services.AddSingleton<IFeatureFlagSource>(custom);

        services.AddOpenApiFeatureFlagsWithFeatureManagement();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IFeatureFlagSource>().ShouldBeSameAs(custom);
    }

    [Fact]
    public void EndToEndThePlannerHonoursMicrosoftsFeatureManager()
    {
        var manager = new FakeFeatureManager().Set("on", true).Set("off", false);

        var services = NewServices();
        services.AddSingleton<IFeatureManager>(manager);
        services.AddOpenApiFeatureFlagsWithFeatureManagement();

        using var provider = services.BuildServiceProvider();
        var planner = provider.GetRequiredService<IDocumentVisibilityPlanner>();

        planner.IsHidden(["on"]).ShouldBeFalse();
        planner.IsHidden(["off"]).ShouldBeTrue();
        planner.IsHidden([]).ShouldBeFalse();

        manager.Reads.ShouldBe(["on", "off"]);
    }

    [Fact]
    public void EndToEndAnUnreachableStoreFailsClosedAndTripsTheCanary()
    {
        var manager = new FakeFeatureManager().Failing("broken");

        var services = NewServices();
        services.AddSingleton<IFeatureManager>(manager);
        services.AddOpenApiFeatureFlagsWithFeatureManagement();

        using var provider = services.BuildServiceProvider();
        var planner = provider.GetRequiredService<IDocumentVisibilityPlanner>();

        planner.IsHidden(["broken"]).ShouldBeTrue();

        Should.Throw<FeatureFlagSourceUnavailableException>(() => planner.CompleteDocument());
    }

    [Fact]
    public void RejectsAMissingServiceCollection()
    {
        Should.Throw<ArgumentNullException>(() =>
            OpenApiFeatureFlagsFeatureManagementExtensions.AddOpenApiFeatureFlagsWithFeatureManagement(null!));
        Should.Throw<ArgumentNullException>(() =>
            OpenApiFeatureFlagsFeatureManagementExtensions.AddFeatureManagementFlagSource(null!));
    }

    private sealed class CustomFlagSource : IFeatureFlagSource
    {
        public bool IsEnabled(string flagName) => true;
    }

    /// <summary>
    /// Mirrors a real host: <c>AddFeatureManagement</c> needs <see cref="IConfiguration"/>, which an
    /// ASP.NET Core application always has.
    /// </summary>
    private static ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return services;
    }
}
