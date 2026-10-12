using FastEndpoints;
using Microsoft.AspNetCore.Builder;

namespace OpenApiFeatureFlags.FastEndpoints.Tests;

/// <summary>The untouched control endpoint.</summary>
public sealed class ControlEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/api/control");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync("control", ct);
}

/// <summary>Gated through endpoint metadata, the route this engine supports.</summary>
public sealed class CheckoutEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Post("/api/checkout");
        AllowAnonymous();

        Options(x => x.WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout")));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync("checkout", ct);
}

/// <summary>
/// Carries a class-level attribute on purpose. The measured behaviour (locked by a test) is that it
/// does NOT gate the endpoint: FastEndpoints does not copy the endpoint class's attributes into the
/// endpoint metadata the adapter reads. docs/fastendpoints.md prescribes the metadata route instead.
/// </summary>
[OpenApiFeatureFlag("ClassFlag")]
public sealed class ClassAttributeEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/api/class-attr");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync("class", ct);
}

public sealed class CreateRequest
{
    public string? Id { get; set; }

    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }
}

public sealed class CreateResponse
{
    public string? Note { get; set; }
}

/// <summary>Exercises DTO (schema) property gating.</summary>
public sealed class CreateEndpoint : Endpoint<CreateRequest, CreateResponse>
{
    public override void Configure()
    {
        Post("/api/orders");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateRequest req, CancellationToken ct) =>
        await Send.OkAsync(new CreateResponse { Note = "created" }, ct);
}

/// <summary>Exercises a gate fragment inside the Summary() string.</summary>
public sealed class SearchEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/api/search");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Search docs. <gate flag=\"BetaDocs\">Beta search notes apply.</gate>";
        });
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync("results", ct);
}

/// <summary>
/// A GET DTO property with a gate. The measured behaviour (locked by a test) is that the query
/// parameter stays: FastEndpoints flattens GET DTO properties into parameters, and a parameter needs
/// the controller path to be removed. docs/fastendpoints.md records the boundary.
/// </summary>
public sealed class ScopedSearchRequest
{
    [OpenApiFeatureFlag("InternalSearch")]
    public string? Scope { get; set; }
}

public sealed class ScopedSearchEndpoint : Endpoint<ScopedSearchRequest, string>
{
    public override void Configure()
    {
        Get("/api/param-search");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ScopedSearchRequest req, CancellationToken ct) =>
        await Send.OkAsync("results", ct);
}
