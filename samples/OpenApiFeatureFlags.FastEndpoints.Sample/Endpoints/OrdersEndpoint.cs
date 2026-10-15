using FastEndpoints;

namespace OpenApiFeatureFlags.FastEndpoints.Sample.Endpoints;

/// <summary>An endpoint with no flag, documented in every flag state.</summary>
public sealed class OrdersEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/api/orders");
        AllowAnonymous();

        Summary(s => s.Summary = "Lists orders. Always documented.");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync("order-1, order-2", ct);
}
