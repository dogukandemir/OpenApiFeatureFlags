using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// Phase 3 behaviours in <see cref="DocumentMode.Remove"/>: the default, and the one that fixes the
/// original customer-facing problem.
/// </summary>
public sealed class SwashbuckleAdapterTests
{
    private const string ShopsPath = "/api/shops/{id}";
    private const string ReportsPath = "/api/shops/reports";
    private const string SearchPath = "/api/shops/search";
    private const string ExperimentalPath = "/api/experimental";

    [Fact]
    public void WithNoGatedElementsTheDocumentIsByteIdentical()
    {
        // The single most important regression test: with nothing gated, the library must be
        // invisible. The flag source throws on every read, which proves it is never consulted.
        var withFilters = TestSwagger.Build(
            StubFlagSource.Unreachable(),
            useFeatureFlagFilters: true,
            controllers: [typeof(PlainController)]);

        var withoutFilters = TestSwagger.Build(
            StubFlagSource.Unreachable(),
            useFeatureFlagFilters: false,
            controllers: [typeof(PlainController)]);

        TestSwagger.ToJson(withFilters).ShouldBe(TestSwagger.ToJson(withoutFilters));
    }

    [Fact]
    public void HidesAnOperationWhoseFlagIsDisabled()
    {
        var document = TestSwagger.Build(new StubFlagSource());

        TestSwagger.HasOperation(document, ReportsPath, HttpMethod.Get).ShouldBeFalse();
    }

    [Fact]
    public void KeepsAnOperationWhoseFlagIsEnabled()
    {
        var document = TestSwagger.Build(new StubFlagSource().WithEnabled("reports"));

        TestSwagger.HasOperation(document, ReportsPath, HttpMethod.Get).ShouldBeTrue();
    }

    [Fact]
    public void HidesEveryOperationOfAControllerLevelFlag()
    {
        var document = TestSwagger.Build(new StubFlagSource());

        TestSwagger.HasOperation(document, ExperimentalPath, HttpMethod.Get).ShouldBeFalse();
        TestSwagger.HasOperation(document, ExperimentalPath, HttpMethod.Post).ShouldBeFalse();
    }

    [Fact]
    public void DropsThePathItemWhenItsLastOperationIsRemoved()
    {
        var document = TestSwagger.Build(new StubFlagSource());

        // The controller's every operation is gated, so the path must not survive as an empty stub.
        TestSwagger.Paths(document).ShouldNotContain(ExperimentalPath);
    }

    [Fact]
    public void KeepsUngatedOperationsOnTheSameController()
    {
        var document = TestSwagger.Build(new StubFlagSource());

        TestSwagger.Paths(document).ShouldContain(ShopsPath);
        TestSwagger.Paths(document).ShouldContain(SearchPath);
        TestSwagger.Paths(document).ShouldContain("/api/shops/health");
    }

    [Fact]
    public void HidesAGatedSchemaPropertyAndKeepsItsSiblings()
    {
        var document = TestSwagger.Build(new StubFlagSource());

        TestSwagger.HasProperty(document, nameof(ShopModel), "loyaltyPoints").ShouldBeFalse();
        TestSwagger.HasProperty(document, nameof(ShopModel), "loyaltyTier").ShouldBeFalse();

        // Ungated siblings survive, which is what makes the feature surgical.
        TestSwagger.HasProperty(document, nameof(ShopModel), "id").ShouldBeTrue();
        TestSwagger.HasProperty(document, nameof(ShopModel), "note").ShouldBeTrue();
    }

    [Fact]
    public void KeepsAGatedSchemaPropertyWhenItsFlagIsEnabled()
    {
        var document = TestSwagger.Build(new StubFlagSource().WithEnabled("loyalty"));

        TestSwagger.HasProperty(document, nameof(ShopModel), "loyaltyPoints").ShouldBeTrue();
        TestSwagger.HasProperty(document, nameof(ShopModel), "loyaltyTier").ShouldBeTrue();
    }

    [Fact]
    public void RemovesThePropertyNameFromTheRequiredList()
    {
        var document = TestSwagger.Build(new StubFlagSource());

        TestSwagger.RequiredProperties(document, nameof(ShopModel)).ShouldNotContain("loyaltyTier");
    }

    [Fact]
    public void StripsStaleRequiredEntriesEvenWhenAnotherFilterAddedThem()
    {
        // BACKLOG.md 4.2: a consumer schema filter written as
        // Required.Add(properties.FirstOrDefault(...).Key) adds a literal null once the property it
        // wanted has been removed. The adapter must not let that reach the published document.
        var document = TestSwagger.Build(
            new StubFlagSource(),
            configureSwagger: options => options.SchemaFilter<StaleRequiredEntrySchemaFilter>());

        var required = TestSwagger.RequiredProperties(document, nameof(ShopModel));

        required.ShouldNotContain(name => string.IsNullOrEmpty(name));
        required.ShouldNotContain("loyaltyTier");
    }

    [Fact]
    public void HidesAGatedActionParameter()
    {
        var document = TestSwagger.Build(new StubFlagSource());

        TestSwagger.ParameterNames(document, SearchPath, HttpMethod.Get).ShouldNotContain("scope");
    }

    [Fact]
    public void KeepsAGatedActionParameterWhenItsFlagIsEnabled()
    {
        var document = TestSwagger.Build(new StubFlagSource().WithEnabled("internal-search"));

        TestSwagger.ParameterNames(document, SearchPath, HttpMethod.Get).ShouldContain("scope");
    }

    [Fact]
    public void DropsAComponentSchemaThatTheRemovalOrphaned()
    {
        var document = TestSwagger.Build(new StubFlagSource());

        // The only reference to AuditInfo was the gated "audit" property.
        TestSwagger.HasComponent(document, nameof(AuditInfo)).ShouldBeFalse();
        TestSwagger.HasProperty(document, nameof(ShopModel), "audit").ShouldBeFalse();
    }

    [Fact]
    public void KeepsTheOrphanedComponentWhenItsFlagIsEnabled()
    {
        var document = TestSwagger.Build(new StubFlagSource().WithEnabled("audit"));

        TestSwagger.HasComponent(document, nameof(AuditInfo)).ShouldBeTrue();
    }

    [Fact]
    public void PrunesTagsThatNoRemainingOperationUses()
    {
        var document = TestSwagger.Build(
            new StubFlagSource(),
            configureSwagger: options => options.DocumentFilter<DeclaredTagsDocumentFilter>());

        var tagNames = TestSwagger.TagNames(document);

        tagNames.ShouldContain("shops");
        tagNames.ShouldNotContain("experimental");
        tagNames.ShouldNotContain("orphan");
    }

    [Fact]
    public void NeverLeavesTheInternalRemovalMarkerInTheDocument()
    {
        var document = TestSwagger.Build(new StubFlagSource());

        TestSwagger.ToJson(document).ShouldNotContain("x-openapifeatureflags");
    }

    [Fact]
    public void ProducesTheSameDocumentEveryTime()
    {
        var first = TestSwagger.ToJson(TestSwagger.Build(new StubFlagSource()));
        var second = TestSwagger.ToJson(TestSwagger.Build(new StubFlagSource()));

        first.ShouldBe(second);
    }

    [Fact]
    public void FailsClosedWhenAFlagCannotBeRead()
    {
        // D5: an unreadable flag hides the surface. The canary also fires here because the source is
        // unreachable for every flag, which is the loud failure D6 wants.
        Should.Throw<FeatureFlagSourceUnavailableException>(() => TestSwagger.Build(StubFlagSource.Unreachable()));
    }

    [Fact]
    public void FailsClosedWithoutTrippingTheCanaryWhenOnlyOneFlagIsUnreadable()
    {
        var document = TestSwagger.Build(
            new StubFlagSource().WithEnabled("loyalty").WithUnreadable("reports", "audit", "internal-search", "experimental-api"));

        // The flags that did answer prove the source is reachable, so the document is published...
        TestSwagger.HasOperation(document, ReportsPath, HttpMethod.Get).ShouldBeFalse();

        // ...with the unreadable ones hidden rather than leaked.
        TestSwagger.HasProperty(document, nameof(ShopModel), "loyaltyPoints").ShouldBeTrue();
    }

    private static OpenApiOperation OperationAt(OpenApiDocument document, string path, HttpMethod method) =>
        TestSwagger.OperationAt(document, path, method);

    /// <summary>Adds tags to the document, including one that no operation uses.</summary>
    private sealed class DeclaredTagsDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Tags ??= new HashSet<OpenApiTag>();
            swaggerDoc.Tags.Add(new OpenApiTag { Name = "shops" });
            swaggerDoc.Tags.Add(new OpenApiTag { Name = "experimental" });
            swaggerDoc.Tags.Add(new OpenApiTag { Name = "orphan" });

            if (swaggerDoc.Paths is null)
            {
                return;
            }

            Tag(swaggerDoc, ReportsPath, "experimental");
            Tag(swaggerDoc, ShopsPath, "shops");
            Tag(swaggerDoc, SearchPath, "shops");
            Tag(swaggerDoc, ExperimentalPath, "experimental");
        }

        private static void Tag(OpenApiDocument document, string path, string tagName)
        {
            if (document.Paths?.TryGetValue(path, out var pathItem) != true || pathItem is not OpenApiPathItem concrete)
            {
                return;
            }

            var operations = concrete.Operations;

            if (operations is null)
            {
                return;
            }

            foreach (var operation in operations.Values)
            {
                operation.Tags ??= new HashSet<OpenApiTagReference>();
                operation.Tags.Add(new OpenApiTagReference(tagName, document, string.Empty));
            }
        }
    }

    /// <summary>Reproduces the real-world filter that adds a stale entry to <c>required</c>.</summary>
    private sealed class StaleRequiredEntrySchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema is OpenApiSchema concrete && context.Type == typeof(ShopModel))
            {
                concrete.Required ??= new HashSet<string>();
                concrete.Required.Add("loyaltyTier");
                concrete.Required.Add(string.Empty);
            }
        }
    }
}
