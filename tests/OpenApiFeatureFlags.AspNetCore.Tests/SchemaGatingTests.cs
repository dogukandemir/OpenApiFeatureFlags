using Microsoft.AspNetCore.Builder;
using Shouldly;

namespace OpenApiFeatureFlags.AspNetCore.Tests;

public sealed class Order
{
    public string? Id { get; set; }

    public int Total { get; set; }

    [OpenApiFeatureFlag("LoyaltyProgram")]
    public int LoyaltyPoints { get; set; }
}

public sealed class StaffNote
{
    public required string Id { get; set; }

    [OpenApiFeatureFlag("StaffOnly")]
    public required string InternalNote { get; set; }
}

/// <summary>
/// Gating a model property.
/// </summary>
/// <remarks>
/// The property name is asserted in the document's own casing, which is what makes these tests a check
/// on the adapter rather than on the test's assumptions: the adapter must work from the engine's JSON
/// name for the member, not from the C# name.
/// </remarks>
public sealed class SchemaGatingTests
{
    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/orders/{id}", (string id) => new Order { Id = id, Total = 1, LoyaltyPoints = 5 });
        app.MapGet("/notes/{id}", (string id) => new StaffNote { Id = id, InternalNote = "internal" });
    }

    [Fact]
    public async Task AGatedPropertyIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapEndpoints: MapEndpoints);

        var json = TestDocument.ToJson(document);

        json.ShouldNotContain("loyaltyPoints");
        json.ShouldContain("\"total\"");
    }

    [Fact]
    public async Task AGatedPropertyIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("LoyaltyProgram"),
            mapEndpoints: MapEndpoints);

        TestDocument.ToJson(document).ShouldContain("loyaltyPoints");
    }

    [Fact]
    public async Task RemovingAPropertyAlsoRemovesItFromRequired()
    {
        // A `required` member is the case where removal can leave a dangling name behind, which would
        // make the document invalid rather than merely wrong.
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapEndpoints: MapEndpoints);

        var json = TestDocument.ToJson(document);

        json.ShouldNotContain("internalNote");
        json.ShouldNotContain("required\":[\"internalNote\"]");
    }

    [Fact]
    public async Task GatingOnePropertyLeavesItsNeighboursAlone()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapEndpoints: MapEndpoints);

        var json = TestDocument.ToJson(document);

        json.ShouldContain("\"id\"");
        json.ShouldContain("\"total\"");
        json.ShouldNotContain("loyaltyPoints");
    }

    [Fact]
    public async Task AnnotateLeavesThePropertyVisible()
    {
        // Annotate is a document-level decision; it must not silently start hiding schema members.
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mode: DocumentMode.Annotate,
            mapEndpoints: MapEndpoints);

        TestDocument.ToJson(document).ShouldContain("loyaltyPoints");
    }
}
