namespace OpenApiFeatureFlags.Tests;

/// <summary>
/// A hand-written flag source: readable, and it can distinguish "the flag is off" from
/// "the flag could not be read", which is the whole point of the fail-closed tests.
/// </summary>
internal sealed class FakeFlagSource : IFeatureFlagSource
{
    private readonly Dictionary<string, bool> _enabled = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);

    public List<string> Reads { get; } = [];

    public FakeFlagSource Enabled(params string[] flagNames)
    {
        foreach (var flagName in flagNames)
        {
            _enabled[flagName] = true;
        }

        return this;
    }

    public FakeFlagSource Disabled(params string[] flagNames)
    {
        foreach (var flagName in flagNames)
        {
            _enabled[flagName] = false;
        }

        return this;
    }

    public FakeFlagSource Unreadable(params string[] flagNames)
    {
        foreach (var flagName in flagNames)
        {
            _unreadable.Add(flagName);
        }

        return this;
    }

    public bool IsEnabled(string flagName)
    {
        Reads.Add(flagName);

        if (_unreadable.Contains(flagName))
        {
            throw new InvalidOperationException($"The flag source is unreachable for '{flagName}'.");
        }

        return _enabled.TryGetValue(flagName, out var enabled) && enabled;
    }
}
