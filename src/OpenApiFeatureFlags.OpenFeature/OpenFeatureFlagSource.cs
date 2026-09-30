using System.Diagnostics.CodeAnalysis;
using OpenFeature;

namespace OpenApiFeatureFlags.OpenFeature;

/// <summary>
/// Adapts the CNCF OpenFeature standard to <see cref="IFeatureFlagSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// The default value passed to OpenFeature is <see langword="false"/>, which is what makes this
/// adapter fail closed without any extra work: a flag the provider does not know, or a provider
/// that is not ready, evaluates to <see langword="false"/> and the element stays hidden.
/// </para>
/// <para>
/// OpenFeature is asynchronous while document engines expose a synchronous pipeline, so the call is
/// blocked on. That is the same trade-off as the Microsoft.FeatureManagement adapter, and the same
/// the same reasoning applies: if a provider turns out to be genuinely slow, an async resolution path
/// is the answer rather than leaking tasks from a filter.
/// </para>
/// </remarks>
public sealed class OpenFeatureFlagSource : IFeatureFlagSource
{
    private readonly IFeatureClient _client;

    /// <summary>
    /// Initialises a new instance of the <see cref="OpenFeatureFlagSource"/> class.
    /// </summary>
    /// <param name="client">The OpenFeature client to evaluate against.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
    public OpenFeatureFlagSource(IFeatureClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Not catching here is deliberate: the core fails closed and the canary needs to see that the provider is unreachable.")]
    public bool IsEnabled(string flagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagName);

        return _client
            .GetBooleanValueAsync(flagName, defaultValue: false)
            .GetAwaiter()
            .GetResult();
    }
}
