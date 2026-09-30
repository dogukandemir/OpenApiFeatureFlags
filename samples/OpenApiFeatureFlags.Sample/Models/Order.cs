namespace OpenApiFeatureFlags.Sample;

public sealed class Order
{
    public string? Id { get; set; }

    /// <summary>Only documented while the loyalty programme has been released.</summary>
    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }

    /// <summary>
    /// The order total.
    /// <gate flag="LoyaltyProgram">See <c>loyaltyPoints</c> for the points balance.</gate>
    /// </summary>
    public decimal Total { get; set; }

    public string? Note { get; set; }
}
