namespace OpenApiFeatureFlags;

/// <summary>
/// Marks a controller, action, model property or parameter as gated by a feature flag
/// in the generated OpenAPI document.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is <b>documentation-only</b> (decision D2). It never changes runtime
/// behaviour: a hidden operation is still served, and a hidden property is still
/// serialised. Gating behaviour at runtime remains the job of the application, for
/// example with <c>Microsoft.FeatureManagement</c>'s <c>[FeatureGate]</c>.
/// </para>
/// <para>
/// When more than one attribute is applied to the same target, the flags are combined
/// with <b>AND</b> semantics: the element is only documented when every flag is enabled
/// (decision D3).
/// </para>
/// <para>
/// The attribute is deliberately <b>not inherited</b>. Placing it on a base class must not
/// silently gate every derived controller.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [OpenApiFeatureFlag("NewCheckout")]
/// [HttpPost("checkout")]
/// public IActionResult Checkout() => Ok();
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter,
    AllowMultiple = true,
    Inherited = false)]
public sealed class OpenApiFeatureFlagAttribute : Attribute
{
    /// <summary>
    /// Initialises a new instance of the <see cref="OpenApiFeatureFlagAttribute"/> class.
    /// </summary>
    /// <param name="flagName">
    /// The name of the feature flag that governs whether the annotated element appears in
    /// the OpenAPI document. The name is matched exactly against
    /// <see cref="IFeatureFlagSource.IsEnabled"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="flagName"/> is <see langword="null"/>, empty or whitespace.
    /// </exception>
    public OpenApiFeatureFlagAttribute(string flagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagName);
        FlagName = flagName;
    }

    /// <summary>
    /// Gets the name of the feature flag that governs this element.
    /// </summary>
    public string FlagName { get; }
}
