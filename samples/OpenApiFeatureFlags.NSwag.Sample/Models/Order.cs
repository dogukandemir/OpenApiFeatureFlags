using System.ComponentModel;

namespace OpenApiFeatureFlags.NSwag.Sample;

public sealed class Order
{
    public string? Id { get; set; }

    /// <summary>Only documented while the loyalty programme has been released.</summary>
    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }

    // A description attribute rather than an XML comment: the gate text reaches the document
    // verbatim this way, while NSwag flattens XML documentation markup (docs/nswag.md).
    [Description("The order total. <gate flag=\"LoyaltyProgram\">See loyaltyPoints for the points balance.</gate>")]
    public decimal Total { get; set; }

    public string? Note { get; set; }
}
