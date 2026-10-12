using OpenApiFeatureFlags.Analyzers;
using Shouldly;

namespace OpenApiFeatureFlags.Analyzers.Tests;

/// <summary>OAFF5001: attributes on FastEndpoints endpoints never reach endpoint metadata.</summary>
public sealed class FastEndpointsTests
{
    [Fact]
    public async Task AnAttributeOnAnEndpointClassIsReported()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using OpenApiFeatureFlags;

            namespace FastEndpoints
            {
                public abstract class EndpointWithoutRequest
                {
                }
            }

            [OpenApiFeatureFlag("NewCheckout")]
            public sealed class CheckoutEndpoint : FastEndpoints.EndpointWithoutRequest
            {
            }
            """,
            referenceCore: true,
            analyzers: [new FastEndpointsAttributeAnalyzer()]);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("OAFF5001");
    }

    [Fact]
    public async Task AnAttributeOnAnEndpointMethodIsReported()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using OpenApiFeatureFlags;

            namespace FastEndpoints
            {
                public abstract class Endpoint<TRequest, TResponse>
                {
                }
            }

            public sealed class OrderEndpoint : FastEndpoints.Endpoint<string, string>
            {
                [OpenApiFeatureFlag("NewCheckout")]
                public void Configure()
                {
                }
            }
            """,
            referenceCore: true,
            analyzers: [new FastEndpointsAttributeAnalyzer()]);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("OAFF5001");
    }

    [Fact]
    public async Task AnAttributeOnAPlainClassIsNotReported()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using OpenApiFeatureFlags;

            [OpenApiFeatureFlag("Anything")]
            public sealed class OrdersController
            {
            }
            """,
            referenceCore: true,
            analyzers: [new FastEndpointsAttributeAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnAttributeOnADtoPropertyIsNotReported()
    {
        // Schema property gating works under FastEndpoints; only endpoint-attached attributes are
        // no-ops.
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using OpenApiFeatureFlags;

            public sealed class CreateOrderRequest
            {
                [OpenApiFeatureFlag("LoyaltyProgram")]
                public int LoyaltyPoints { get; set; }
            }
            """,
            referenceCore: true,
            analyzers: [new FastEndpointsAttributeAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }
}
