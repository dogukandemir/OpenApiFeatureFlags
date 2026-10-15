using FastEndpoints;
using OpenApiFeatureFlags;

namespace OpenApiFeatureFlags.FastEndpoints.Sample.Endpoints;

/// <summary>
/// Documented only once the new checkout has been released. FastEndpoints does not carry attributes
/// on the endpoint or its members into endpoint metadata, so the flag goes in as metadata.
/// </summary>
public sealed class CheckoutEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Post("/api/orders/checkout");
        AllowAnonymous();

        // The one supported route for gating a FastEndpoints operation. An
        // [OpenApiFeatureFlag] on the class or on a member never reaches the document (OAFF5001).
        Options(x => x.WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout")));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync("checked out", ct);
}
