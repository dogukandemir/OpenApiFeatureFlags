using Shouldly;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>
/// Gating controller actions, through the action method's <c>MethodInfo</c>.
/// </summary>
public sealed class ControllerGatingTests
{
    [Fact]
    public async Task AGatedActionIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/widgets", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/widgets/checkout", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task AGatedActionIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("NewCheckout"),
            mapControllers: true);

        document.RootElement.GetProperty("paths").TryGetProperty("/api/widgets/checkout", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AControllerLevelFlagGatesEveryAction()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/legacy", out _).ShouldBeFalse();
        paths.TryGetProperty("/api/legacy/export", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task AControllerLevelFlagAndAnActionFlagAreAnded()
    {
        // Legacy is on but Export is off: the class flag alone must not publish the export action,
        // and the export flag alone must not hide the plain one.
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("Legacy"),
            mapControllers: true);

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/legacy", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/legacy/export", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WithBothFlagsOnEveryActionIsPublished()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("Legacy", "Export"),
            mapControllers: true);

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/legacy", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/legacy/export", out _).ShouldBeTrue();
    }
}
