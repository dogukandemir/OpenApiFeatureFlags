using System.Reflection;
using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.Tests;

public sealed class AttributeUsageTests
{
    [Fact]
    public void CanBeAppliedToAController_Action_PropertyAndParameter()
    {
        typeof(GatedController).GetCustomAttribute<OpenApiFeatureFlagAttribute>().ShouldNotBeNull()
            .FlagName.ShouldBe("controller-flag");

        typeof(GatedController).GetMethod(nameof(GatedController.Act))!
            .GetCustomAttribute<OpenApiFeatureFlagAttribute>().ShouldNotBeNull()
            .FlagName.ShouldBe("action-flag");

        typeof(GatedModel).GetProperty(nameof(GatedModel.Secret))!
            .GetCustomAttribute<OpenApiFeatureFlagAttribute>().ShouldNotBeNull()
            .FlagName.ShouldBe("property-flag");

        typeof(GatedController).GetMethod(nameof(GatedController.Act))!
            .GetParameters()[0]
            .GetCustomAttribute<OpenApiFeatureFlagAttribute>().ShouldNotBeNull()
            .FlagName.ShouldBe("parameter-flag");
    }

    [Fact]
    public void AllowsSeveralFlagsOnOneTarget()
    {
        var flags = typeof(GatedModel).GetProperty(nameof(GatedModel.DoubleGated))!
            .GetCustomAttributes<OpenApiFeatureFlagAttribute>()
            .Select(a => a.FlagName)
            .ToArray();

        // Multiple attributes mean AND semantics (D3): every flag must be enabled.
        flags.ShouldBe(["feature-exists", "present-value"], ignoreOrder: true);
    }

    [Fact]
    public void IsNotInheritedByDerivedTypes()
    {
        typeof(DerivedController).GetCustomAttributes<OpenApiFeatureFlagAttribute>(inherit: true)
            .ShouldBeEmpty();

        typeof(DerivedController).GetCustomAttributes<OpenApiFeatureFlagAttribute>(inherit: false)
            .ShouldBeEmpty();
    }

    [Fact]
    public void DeclaresTheMetadataContractThatIsAPermanentApi()
    {
        var usage = typeof(OpenApiFeatureFlagAttribute).GetCustomAttribute<AttributeUsageAttribute>()
            .ShouldNotBeNull();

        usage.ValidOn.ShouldBe(
            AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter);
        usage.AllowMultiple.ShouldBeTrue();
        usage.Inherited.ShouldBeFalse();
    }

    [Fact]
    public void IsSealedSoNoSuffixTypeCanChangeAttributeSemantics()
    {
        typeof(OpenApiFeatureFlagAttribute).IsSealed.ShouldBeTrue();
        typeof(OpenApiFeatureFlagAttribute).IsSubclassOf(typeof(Attribute)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsBlankFlagNames(string? flagName)
    {
        Should.Throw<ArgumentException>(() => new OpenApiFeatureFlagAttribute(flagName!));
    }

    [Fact]
    public void KeepsTheFlagNameVerbatim()
    {
        new OpenApiFeatureFlagAttribute("  Odd Flag  ").FlagName.ShouldBe("  Odd Flag  ");
    }

    [OpenApiFeatureFlag("controller-flag")]
    private sealed class GatedController
    {
        [OpenApiFeatureFlag("action-flag")]
        public void Act([OpenApiFeatureFlag("parameter-flag")] string value) => _ = value;
    }

    [OpenApiFeatureFlag("base-flag")]
    private class BaseController;

    private sealed class DerivedController : BaseController;

    private sealed class GatedModel
    {
        [OpenApiFeatureFlag("property-flag")]
        public string? Secret { get; set; }

        [OpenApiFeatureFlag("feature-exists")]
        [OpenApiFeatureFlag("present-value")]
        public string? DoubleGated { get; set; }
    }
}
