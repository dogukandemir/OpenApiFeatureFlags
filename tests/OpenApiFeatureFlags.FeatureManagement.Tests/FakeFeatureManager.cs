using Microsoft.FeatureManagement;

namespace OpenApiFeatureFlags.FeatureManagement.Tests;

/// <summary>Hand-written <see cref="IFeatureManager"/> that can also fail, like a broken store.</summary>
internal sealed class FakeFeatureManager : IFeatureManager
{
    private readonly Dictionary<string, bool> _enabled = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failing = new(StringComparer.Ordinal);

    public List<string> Reads { get; } = [];

    public FakeFeatureManager Set(string flagName, bool enabled)
    {
        _enabled[flagName] = enabled;
        return this;
    }

    public FakeFeatureManager Failing(string flagName)
    {
        _failing.Add(flagName);
        return this;
    }

    public Task<bool> IsEnabledAsync(string feature)
    {
        Reads.Add(feature);

        if (_failing.Contains(feature))
        {
            throw new FeatureManagementException(
                FeatureManagementError.Conflict,
                $"The store could not be reached for '{feature}'.");
        }

        return Task.FromResult(_enabled.TryGetValue(feature, out var enabled) && enabled);
    }

    public Task<bool> IsEnabledAsync<TContext>(string feature, TContext context)
    {
        _ = context;
        return IsEnabledAsync(feature);
    }

    public async IAsyncEnumerable<string> GetFeatureNamesAsync()
    {
        foreach (var name in _enabled.Keys.ToArray())
        {
            yield return name;
        }

        await Task.CompletedTask;
    }
}
