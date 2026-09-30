namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>Flag source that can be made to fail, so fail-closed behaviour is testable.</summary>
internal sealed class StubFlagSource : IFeatureFlagSource
{
    private readonly HashSet<string> _enabled = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);

    public List<string> Reads { get; } = [];

    /// <summary>A source where no flag can be read: the fail-closed and canary worst case.</summary>
    public static StubFlagSource Unreachable() => new StubFlagSource().WithUnreadable("*");

    public StubFlagSource WithEnabled(params string[] flagNames) => With(_enabled, flagNames);

    public StubFlagSource WithUnreadable(params string[] flagNames) => With(_unreadable, flagNames);

    public bool IsEnabled(string flagName)
    {
        Reads.Add(flagName);

        if (_unreadable.Contains(flagName) || _unreadable.Contains("*"))
        {
            throw new InvalidOperationException($"Flag source is unreachable for '{flagName}'.");
        }

        return _enabled.Contains(flagName);
    }

    private StubFlagSource With(HashSet<string> target, string[] values)
    {
        foreach (var value in values)
        {
            target.Add(value);
        }

        return this;
    }
}
