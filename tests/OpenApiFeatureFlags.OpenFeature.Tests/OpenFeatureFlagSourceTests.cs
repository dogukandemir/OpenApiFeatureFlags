using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.OpenFeature.Tests;

public sealed class OpenFeatureFlagSourceTests
{
    [Fact]
    public void ReportsTheProvidersAnswer()
    {
        var client = new FakeFeatureClient().Set("on", true).Set("off", false);
        var source = new OpenFeatureFlagSource(client);

        source.IsEnabled("on").ShouldBeTrue();
        source.IsEnabled("off").ShouldBeFalse();
        client.Reads.ShouldBe(["on", "off"]);
    }

    [Fact]
    public void ReportsFalseForAFlagTheProviderDoesNotKnow()
    {
        var source = new OpenFeatureFlagSource(new FakeFeatureClient());

        // The adapter passes false as OpenFeature's default value, which is what makes it fail
        // closed (D5) for free: an unknown flag cannot leak an element.
        source.IsEnabled("never-configured").ShouldBeFalse();
    }

    [Fact]
    public void LetsProviderFailuresEscalateSoTheCanaryCanFire()
    {
        var source = new OpenFeatureFlagSource(new FakeFeatureClient().Failing("broken"));

        // Swallowing this would make an unreachable provider look like "every flag is off".
        Should.Throw<InvalidOperationException>(() => source.IsEnabled("broken"));
    }

    [Fact]
    public void RejectsBlankFlagNames()
    {
        var source = new OpenFeatureFlagSource(new FakeFeatureClient());

        Should.Throw<ArgumentException>(() => source.IsEnabled(null!));
        Should.Throw<ArgumentException>(() => source.IsEnabled("  "));
    }

    [Fact]
    public void RejectsAMissingClient()
    {
        Should.Throw<ArgumentNullException>(() => new OpenFeatureFlagSource(null!));
    }
}
