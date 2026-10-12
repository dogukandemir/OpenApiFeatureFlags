namespace OpenApiFeatureFlags.FastEndpoints.Tests;

/// <summary>A flag source that answers from a fixed set of enabled names.</summary>
internal sealed class FakeFlagSource : IFeatureFlagSource
{
    private readonly HashSet<string> _enabled;

    public FakeFlagSource(params string[] enabled) =>
        _enabled = new HashSet<string>(enabled, StringComparer.Ordinal);

    public bool IsEnabled(string flagName) => _enabled.Contains(flagName);
}
