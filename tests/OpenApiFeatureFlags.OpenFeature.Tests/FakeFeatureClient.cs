using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace OpenApiFeatureFlags.OpenFeature.Tests;

/// <summary>
/// Hand-written <see cref="IFeatureClient"/>. Only the boolean path is implemented; everything else
/// throws, so the tests cannot accidentally depend on behaviour this adapter does not use.
/// </summary>
internal sealed class FakeFeatureClient : IFeatureClient
{
    private readonly Dictionary<string, bool> _enabled = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failing = new(StringComparer.Ordinal);

    public List<string> Reads { get; } = [];

    public ProviderStatus ProviderStatus => ProviderStatus.Ready;

    public FakeFeatureClient Set(string flagName, bool enabled)
    {
        _enabled[flagName] = enabled;
        return this;
    }

    public FakeFeatureClient Failing(string flagName)
    {
        _failing.Add(flagName);
        return this;
    }

    public Task<bool> GetBooleanValueAsync(
        string flagKey,
        bool defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _ = context;
        _ = options;
        _ = cancellationToken;

        Reads.Add(flagKey);

        if (_failing.Contains(flagKey))
        {
            throw new InvalidOperationException($"provider is down for '{flagKey}'");
        }

        return Task.FromResult(_enabled.TryGetValue(flagKey, out var enabled) ? enabled : defaultValue);
    }

    public void AddHooks(IEnumerable<Hook> hooks) => throw new NotSupportedException();

    public IEnumerable<Hook> GetHooks() => throw new NotSupportedException();

    public EvaluationContext GetContext() => throw new NotSupportedException();

    public void AddHandler(ProviderEventTypes eventType, EventHandlerDelegate handler) => throw new NotSupportedException();

    public void RemoveHandler(ProviderEventTypes eventType, EventHandlerDelegate handler) => throw new NotSupportedException();

    public void SetContext(EvaluationContext? context) => throw new NotSupportedException();

    public ClientMetadata GetMetadata() => throw new NotSupportedException();

    public Task<FlagEvaluationDetails<bool>> GetBooleanDetailsAsync(
        string flagKey,
        bool defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<string> GetStringValueAsync(
        string flagKey,
        string defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<FlagEvaluationDetails<string>> GetStringDetailsAsync(
        string flagKey,
        string defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> GetIntegerValueAsync(
        string flagKey,
        int defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<FlagEvaluationDetails<int>> GetIntegerDetailsAsync(
        string flagKey,
        int defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<double> GetDoubleValueAsync(
        string flagKey,
        double defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<FlagEvaluationDetails<double>> GetDoubleDetailsAsync(
        string flagKey,
        double defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Value> GetObjectValueAsync(
        string flagKey,
        Value defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<FlagEvaluationDetails<Value>> GetObjectDetailsAsync(
        string flagKey,
        Value defaultValue,
        EvaluationContext? context = null,
        FlagEvaluationOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public void Track(string trackingEventName, EvaluationContext? context = null, TrackingEventDetails? details = null)
        => throw new NotSupportedException();
}
