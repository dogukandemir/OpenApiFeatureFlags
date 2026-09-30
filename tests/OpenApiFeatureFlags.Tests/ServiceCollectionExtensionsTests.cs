using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void RegistersThePlannerWithASuppliedFlagSource()
    {
        var source = new FakeFlagSource().Enabled("a");

        using var provider = BuildProvider(services =>
            services.AddOpenApiFeatureFlags(source, options => options.Mode = DocumentMode.Include));

        provider.GetRequiredService<IFeatureFlagSource>().ShouldBeSameAs(source);
        provider.GetRequiredService<IDocumentVisibilityPlanner>().Mode.ShouldBe(DocumentMode.Include);
    }

    [Fact]
    public void DefaultsToRemoveMode()
    {
        using var provider = BuildProvider(services => services.AddOpenApiFeatureFlags(new FakeFlagSource()));

        provider.GetRequiredService<IDocumentVisibilityPlanner>().Mode.ShouldBe(DocumentMode.Remove);
    }

    [Fact]
    public void EnablesTheCanaryByDefault()
    {
        using var provider = BuildProvider(services => services.AddOpenApiFeatureFlags(new FakeFlagSource()));

        var options = provider.GetRequiredService<IOptions<OpenApiFeatureFlagsOptions>>().Value;

        options.CanaryEnabled.ShouldBeTrue();
    }

    [Fact]
    public void RegistersHttpContextAccessorSoMemoisationWorks()
    {
        using var provider = BuildProvider(services => services.AddOpenApiFeatureFlags(new FakeFlagSource()));

        provider.GetRequiredService<IHttpContextAccessor>().ShouldNotBeNull();
    }

    [Fact]
    public void RegistersThePlannerAsASingletonSoFiltersCanInjectIt()
    {
        // Document engines build filters once and can only inject singletons (4.1).
        using var provider = BuildProvider(services => services.AddOpenApiFeatureFlags(new FakeFlagSource()));

        provider.GetRequiredService<IDocumentVisibilityPlanner>()
            .ShouldBeSameAs(provider.GetRequiredService<IDocumentVisibilityPlanner>());
    }

    [Fact]
    public void AllowsTheFlagSourceToBeRegisteredSeparately()
    {
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IFeatureFlagSource>(new FakeFlagSource().Enabled("a"));
            services.AddOpenApiFeatureFlags();
        });

        provider.GetRequiredService<IDocumentVisibilityPlanner>().ShouldNotBeNull();
    }

    [Fact]
    public void FailsClearlyWhenNoFlagSourceIsRegistered()
    {
        using var provider = BuildProvider(services => services.AddOpenApiFeatureFlags());

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IDocumentVisibilityPlanner>());
    }

    [Fact]
    public void DoesNotOverwriteAFlagSourceRegisteredEarlier()
    {
        var first = new FakeFlagSource();
        var second = new FakeFlagSource();

        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IFeatureFlagSource>(first);
            services.AddOpenApiFeatureFlags(second);
        });

        provider.GetRequiredService<IFeatureFlagSource>().ShouldBeSameAs(first);
    }

    [Fact]
    public void ReturnsTheSameCollectionForChaining()
    {
        var services = new ServiceCollection();

        services.AddOpenApiFeatureFlags(new FakeFlagSource()).ShouldBeSameAs(services);
    }

    [Fact]
    public void RejectsNulls()
    {
        Should.Throw<ArgumentNullException>(() =>
            OpenApiFeatureFlagsServiceCollectionExtensions.AddOpenApiFeatureFlags(null!, (IFeatureFlagSource)null!));
        Should.Throw<ArgumentNullException>(() =>
            new ServiceCollection().AddOpenApiFeatureFlags((IFeatureFlagSource)null!));
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        return services.BuildServiceProvider();
    }
}
