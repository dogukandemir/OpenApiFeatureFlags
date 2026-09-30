using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// <see cref="DocumentMode.Annotate"/> and <see cref="DocumentMode.Include"/>, plus the filter
/// ordering contract the adapter relies on.
/// </summary>
public sealed class SwashbuckleAdapterModeTests
{
    private const string ReportsPath = "/api/shops/reports";
    private const string ShopsPath = "/api/shops/{id}";

    [Fact]
    public void AnnotateModeRemovesNothing()
    {
        var document = TestSwagger.Build(new StubFlagSource(), DocumentMode.Annotate);

        TestSwagger.HasOperation(document, ReportsPath, HttpMethod.Get).ShouldBeTrue();
        TestSwagger.HasProperty(document, nameof(ShopModel), "loyaltyPoints").ShouldBeTrue();
        TestSwagger.HasComponent(document, nameof(AuditInfo)).ShouldBeTrue();
    }

    [Fact]
    public void AnnotateModeTagsGatedOperationsWithTheFlagsThatGovernThem()
    {
        var document = TestSwagger.Build(new StubFlagSource(), DocumentMode.Annotate);

        var extensions = TestSwagger.OperationAt(document, ReportsPath, HttpMethod.Get).Extensions;

        extensions.ShouldNotBeNull();
        extensions.ShouldContainKey("x-feature-flag");
        TestSwagger.ToJson(document).ShouldContain("\"x-feature-flag\"");
    }

    [Fact]
    public void AnnotateModePublishesADocumentLevelFlagMap()
    {
        // The exact shape is still open (Q3); today the document lists every flag it knows about.
        var document = TestSwagger.Build(new StubFlagSource(), DocumentMode.Annotate);

        var extensions = document.Extensions;

        extensions.ShouldNotBeNull();
        extensions.ShouldContainKey("x-feature-flag");

        var json = TestSwagger.ToJson(document);
        json.ShouldContain("experimental-api");
        json.ShouldContain("internal-search");
    }

    [Fact]
    public void AnnotateModeNeverConsultsTheFlagSource()
    {
        // Annotation needs the flag names, not their state, so an unreachable source cannot damage
        // an annotated document.
        var source = StubFlagSource.Unreachable();

        var document = TestSwagger.Build(source, DocumentMode.Annotate);

        TestSwagger.HasOperation(document, ReportsPath, HttpMethod.Get).ShouldBeTrue();
        source.Reads.ShouldBeEmpty();
    }

    [Fact]
    public void IncludeModePublishesTheDocumentUnchanged()
    {
        // The per-environment off switch: the document must be exactly what the engine produced,
        // even though every flag is disabled and the source throws on any read.
        Type[] controllers = [typeof(ShopController), typeof(ExperimentalController), typeof(PlainController)];

        var withFilters = TestSwagger.Build(
            StubFlagSource.Unreachable(),
            DocumentMode.Include,
            useFeatureFlagFilters: true,
            controllers: controllers);

        var withoutFilters = TestSwagger.Build(
            StubFlagSource.Unreachable(),
            DocumentMode.Include,
            useFeatureFlagFilters: false,
            controllers: controllers);

        TestSwagger.ToJson(withFilters).ShouldBe(TestSwagger.ToJson(withoutFilters));
    }

    [Fact]
    public void IncludeModeNeverConsultsTheFlagSource()
    {
        var source = StubFlagSource.Unreachable();

        TestSwagger.Build(source, DocumentMode.Include);

        source.Reads.ShouldBeEmpty();
    }

    [Fact]
    public void OperationPruningRunsAfterAConsumerProcessorThatRebuildsPaths()
    {
        // A processor that clears and rebuilds Paths would re-add removed operations,
        // so operation pruning has to survive it. Registering our filters last is what guarantees it.
        var document = TestSwagger.Build(
            new StubFlagSource(),
            configureSwagger: options => options.DocumentFilter<RebuildingTagsDocumentFilter>());

        TestSwagger.HasOperation(document, ReportsPath, HttpMethod.Get).ShouldBeFalse();
    }

    [Fact]
    public void TagPruningRunsAfterAConsumerProcessorThatRebuildsTags()
    {
        // The mirror image: the processor below rebuilds Tags (including an orphan), so this only
        // passes when tag pruning is registered after it. A single document filter doing both jobs
        // would have its tag work silently undone.
        var document = TestSwagger.Build(
            new StubFlagSource(),
            configureSwagger: options => options.DocumentFilter<RebuildingTagsDocumentFilter>());

        var tagNames = TestSwagger.TagNames(document);

        tagNames.ShouldContain("shops");
        tagNames.ShouldNotContain("orphan");
    }

    [Fact]
    public void KeepsTheTagOfAnOperationThatSurvived()
    {
        var document = TestSwagger.Build(
            new StubFlagSource(),
            configureSwagger: options => options.DocumentFilter<RebuildingTagsDocumentFilter>());

        TestSwagger.HasOperation(document, ShopsPath, HttpMethod.Get).ShouldBeTrue();
        TestSwagger.TagNames(document).ShouldContain("shops");
    }

    /// <summary>
    /// Stands in for the real consumer processor: it clears and rebuilds the document's tags from the
    /// operations, and always adds one tag that nothing references.
    /// </summary>
    private sealed class RebuildingTagsDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Tags = new HashSet<OpenApiTag> { new() { Name = "orphan" } };

            if (swaggerDoc.Paths is null)
            {
                return;
            }

            foreach (var path in swaggerDoc.Paths.ToArray())
            {
                if (path.Value is not OpenApiPathItem pathItem || pathItem.Operations is null)
                {
                    continue;
                }

                foreach (var operation in pathItem.Operations.Values)
                {
                    operation.Tags = new HashSet<OpenApiTagReference>();

                    if (path.Key.StartsWith("/api/shops", StringComparison.Ordinal))
                    {
                        swaggerDoc.Tags.Add(new OpenApiTag { Name = "shops" });
                        operation.Tags.Add(new OpenApiTagReference("shops", swaggerDoc, string.Empty));
                    }
                }
            }
        }
    }
}
