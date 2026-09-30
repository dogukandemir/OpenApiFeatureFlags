using System.Reflection;
using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.Tests;

public sealed class OpenApiFeatureFlagDiscoveryTests
{
    [Fact]
    public void ReadsFlagsFromAType()
    {
        OpenApiFeatureFlagDiscovery.ForType(typeof(GatedController)).ShouldBe(["controller-flag"]);
    }

    [Fact]
    public void ReadsFlagsFromAMember()
    {
        OpenApiFeatureFlagDiscovery.ForMember(Property(nameof(GatedModel.Secret)))
            .ShouldBe(["property-flag"]);
    }

    [Fact]
    public void ReadsFlagsFromAParameter()
    {
        var parameter = typeof(GatedController)
            .GetMethod(nameof(GatedController.Act))!
            .GetParameters()[0];

        OpenApiFeatureFlagDiscovery.ForParameter(parameter).ShouldBe(["parameter-flag"]);
    }

    [Fact]
    public void CombinesControllerAndActionFlagsForAnOperation()
    {
        var action = typeof(GatedController).GetMethod(nameof(GatedController.Act))!;

        OpenApiFeatureFlagDiscovery.ForOperation(action).ShouldBe(["action-flag", "controller-flag"]);
    }

    [Fact]
    public void DeduplicatesAndOrdersOrdinally()
    {
        // Reflection does not promise a stable attribute order, so the result is sorted to keep
        // documents and log lines byte-stable.
        var action = typeof(DuplicateController).GetMethod(nameof(DuplicateController.Act))!;

        OpenApiFeatureFlagDiscovery.ForOperation(action).ShouldBe(["alpha", "zeta"]);
    }

    [Fact]
    public void IgnoresAttributesOnBaseTypes()
    {
        OpenApiFeatureFlagDiscovery.ForType(typeof(DerivedController)).ShouldBeEmpty();
    }

    [Fact]
    public void ReturnsEmptyForUngatedMembers()
    {
        OpenApiFeatureFlagDiscovery.ForMember(Property(nameof(GatedModel.Plain))).ShouldBeEmpty();
    }

    [Fact]
    public void DoesNotTreatAnActionParameterAsAnOperationFlag()
    {
        // A parameter-level flag gates that parameter, not the whole operation.
        var action = typeof(GatedController).GetMethod(nameof(GatedController.Act))!;

        OpenApiFeatureFlagDiscovery.ForOperation(action).ShouldNotContain("parameter-flag");
    }

    [Fact]
    public void RejectsNulls()
    {
        Should.Throw<ArgumentNullException>(() => OpenApiFeatureFlagDiscovery.ForType(null!));
        Should.Throw<ArgumentNullException>(() => OpenApiFeatureFlagDiscovery.ForMember(null!));
        Should.Throw<ArgumentNullException>(() => OpenApiFeatureFlagDiscovery.ForParameter(null!));
        Should.Throw<ArgumentNullException>(() => OpenApiFeatureFlagDiscovery.ForOperation(null!));
    }

    private static PropertyInfo Property(string name) =>
        typeof(GatedModel).GetProperty(name)!;

    [OpenApiFeatureFlag("controller-flag")]
    private sealed class GatedController
    {
        [OpenApiFeatureFlag("action-flag")]
        public void Act([OpenApiFeatureFlag("parameter-flag")] string value) => _ = value;
    }

    [OpenApiFeatureFlag("zeta")]
    private sealed class DuplicateController
    {
        [OpenApiFeatureFlag("alpha")]
        [OpenApiFeatureFlag("zeta")]
        public void Act()
        {
        }
    }

    [OpenApiFeatureFlag("base-flag")]
    private class BaseController;

    private sealed class DerivedController : BaseController;

    private sealed class GatedModel
    {
        [OpenApiFeatureFlag("property-flag")]
        public string? Secret { get; set; }

        public string? Plain { get; set; }
    }
}
