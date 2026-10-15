using FastEndpoints;
using OpenApiFeatureFlags.FastEndpoints.Sample.Contracts;

namespace OpenApiFeatureFlags.FastEndpoints.Sample.Endpoints;

/// <summary>Creates an order. Its request schema carries a gated property and gated prose.</summary>
public sealed class CreateOrderEndpoint : Endpoint<CreateOrderRequest, string>
{
    public override void Configure()
    {
        Post("/api/orders");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Creates an order.";
            s.Description = "Points earned are returned <gate flag=\"LoyaltyProgram\">once the loyalty programme has been released</gate>.";
        });
    }

    public override async Task HandleAsync(CreateOrderRequest req, CancellationToken ct) =>
        await Send.OkAsync(req.Id ?? "created", ct);
}
