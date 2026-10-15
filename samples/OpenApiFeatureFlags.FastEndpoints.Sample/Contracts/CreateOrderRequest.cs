using OpenApiFeatureFlags;

namespace OpenApiFeatureFlags.FastEndpoints.Sample.Contracts;

/// <summary>The create-order request. One property is gated.</summary>
public sealed class CreateOrderRequest
{
    public string? Id { get; set; }

    /// <summary>Gated by the loyalty programme flag.</summary>
    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }
}
