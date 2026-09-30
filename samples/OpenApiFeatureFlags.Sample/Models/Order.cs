namespace OpenApiFeatureFlags.Sample;

public sealed class Order
{
    public string? Id { get; set; }

    /// <summary>Only documented while the loyalty programme has been released.</summary>
    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }

    public string? Note { get; set; }
}
