using Microsoft.FeatureManagement;
using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.FeatureManagement.Tests;

public sealed class FeatureManagerFlagSourceTests
{
    [Fact]
    public void ReportsTheManagersAnswer()
    {
        var manager = new FakeFeatureManager().Set("on", true).Set("off", false);
        var source = new FeatureManagerFlagSource(manager);

        source.IsEnabled("on").ShouldBeTrue();
        source.IsEnabled("off").ShouldBeFalse();
        manager.Reads.ShouldBe(["on", "off"]);
    }

    [Fact]
    public void ReportsAFalseForAFeatureThatIsNotDefined()
    {
        var source = new FeatureManagerFlagSource(new FakeFeatureManager());

        // Fail-closed: an undefined flag hides the surface rather than leaking it.
        source.IsEnabled("never-configured").ShouldBeFalse();
    }

    [Fact]
    public void LetsFailuresEscalateSoTheCoreCanFailClosedAndTheCanaryCanFire()
    {
        var manager = new FakeFeatureManager().Failing("broken");
        var source = new FeatureManagerFlagSource(manager);

        // Swallowing this here would make a broken store indistinguishable from "every flag is off",
        // which is exactly the dangerous case the canary exists to catch.
        Should.Throw<FeatureManagementException>(() => source.IsEnabled("broken"));
    }

    [Fact]
    public void RejectsBlankFlagNames()
    {
        var source = new FeatureManagerFlagSource(new FakeFeatureManager());

        Should.Throw<ArgumentException>(() => source.IsEnabled(null!));
        Should.Throw<ArgumentException>(() => source.IsEnabled("  "));
    }

    [Fact]
    public void RejectsAMissingManager()
    {
        Should.Throw<ArgumentNullException>(() => new FeatureManagerFlagSource(null!));
    }
}
