using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// End-to-end proof that a <c>&lt;gate&gt;</c> written in a normal XML doc comment reaches the
/// published document and is resolved there.
/// </summary>
public sealed class DescriptionGateTests
{
    private const string Path = "/api/descriptions";
    private const string NestedPath = "/api/descriptions/nested";
    private const string BrokenPath = "/api/descriptions/broken";
    private const string Model = nameof(GatedDescriptionModel);
    private const string GatedSentence = "Loyalty details are included.";

    [Fact]
    public void HidesAGatedFragmentInAnOperationSummary()
    {
        var text = TextOf(Operation(Build()));

        text.ShouldContain("Returns the model.");
        text.ShouldNotContain(GatedSentence);
        text.ShouldNotContain("<gate");
    }

    [Fact]
    public void KeepsAGatedFragmentWhenItsFlagIsEnabled()
    {
        var text = TextOf(Operation(Build(["loyalty"])));

        text.ShouldContain("Returns the model.");
        text.ShouldContain(GatedSentence);
        text.ShouldNotContain("<gate");
    }

    [Fact]
    public void HidesAGatedFragmentInAPropertyDescription()
    {
        var description = DescriptionOf(Build(), Model, "balance");

        description.ShouldContain("Balance:");
        description.ShouldNotContain("loyalty points are included.");
    }

    [Fact]
    public void KeepsAGatedFragmentInAPropertyDescriptionWhenItsFlagIsEnabled() =>
        DescriptionOf(Build(["loyalty"]), Model, "balance").ShouldContain("loyalty points are included.");

    [Fact]
    public void HidesTheContentOfAGateWithNoFlagAttribute() =>
        // Valid XML in a real doc comment, but not a usable gate: fail closed.
        TextOf(Operation(Build(), BrokenPath)).ShouldNotContain("no flag attribute here");

    [Fact]
    public void NestedGatesInRealCommentsRequireEveryFlag()
    {
        TextOf(Operation(Build(), NestedPath)).ShouldNotContain("outer");

        var outerOnly = TextOf(Operation(Build(["outer"]), NestedPath));
        outerOnly.ShouldContain("outer");
        outerOnly.ShouldNotContain("-inner");

        TextOf(Operation(Build(["outer", "inner"]), NestedPath)).ShouldContain("outer-inner");
    }

    [Theory]
    [InlineData(DocumentMode.Remove)]
    [InlineData(DocumentMode.Annotate)]
    [InlineData(DocumentMode.Include)]
    public void NeverPublishesTheWrapperInAnyDescription(DocumentMode mode)
    {
        var document = Build(mode: mode);

        foreach (var description in AllDescriptions(document))
        {
            description.ShouldNotContain("<gate");
            description.ShouldNotContain("</gate>");
        }

        // '<' is escaped by the serialiser, so a leaked wrapper would show up in one of these forms.
        var json = TestSwagger.ToJson(document);
        json.ShouldNotContain("<gate");
        json.ShouldNotContain("\\u003Cgate");
        json.ShouldNotContain("\\u003cgate");
    }

    [Fact]
    public void GatingThePropertyAndTheProseAboutItTogetherLeavesNoDanglingReference()
    {
        // The property goes because it is gated, and the
        // sentence that named it goes too because it is gated by the same flag.
        var document = Build();

        TestSwagger.HasProperty(document, Model, "gated").ShouldBeFalse();
        DescriptionOf(document, Model, "referring").ShouldNotContain("Gated");
    }

    [Fact]
    public void ADanglingReferenceSurvivesWhenTheAuthorDidNotGateTheProse()
    {
        // The honest limit: the library cannot tell that a sentence refers to a removed member. This
        // pins that behaviour so it cannot change silently, and shows that the fix is to gate the
        // prose too, as the previous test does.
        var document = Build(additionalFilters: options =>
            options.DocumentFilter<DanglingReferenceDocumentFilter>());

        TestSwagger.HasProperty(document, Model, "gated").ShouldBeFalse();
        DescriptionOf(document, Model, "always").ShouldContain("Gated");
    }

    [Fact]
    public void AnUnbalancedGateInjectedByAnEarlierFilterHidesTheRestOfTheText()
    {
        // Cannot be written in a doc comment, because the compiler rejects malformed XML there.
        var text = TextOf(Operation(
            Build(additionalFilters: options => options.DocumentFilter<UnbalancedGateDocumentFilter>())));

        text.ShouldContain("Always present.");
        text.ShouldNotContain("and this bit is gated");
    }

    private static OpenApiDocument Build(
        string[]? enabledFlags = null,
        DocumentMode mode = DocumentMode.Remove,
        Action<SwaggerGenOptions>? additionalFilters = null) =>
        TestSwagger.Build(
            new StubFlagSource().WithEnabled(enabledFlags ?? []),
            mode,
            controllers: [typeof(GatedDescriptionController)],
            configureSwagger: options =>
            {
                options.IncludeXmlComments(typeof(TestSwagger).Assembly);

                // Registered before the library's filters, which is the documented contract.
                additionalFilters?.Invoke(options);
            });

    private static OpenApiOperation Operation(OpenApiDocument document, string path = Path) =>
        TestSwagger.OperationAt(document, path, HttpMethod.Get);

    private static string TextOf(OpenApiOperation operation) =>
        (operation.Summary ?? string.Empty) + "\n" + (operation.Description ?? string.Empty);

    private static string DescriptionOf(OpenApiDocument document, string schemaName, string propertyName)
    {
        var property = TestSwagger.Schema(document, schemaName).Properties![propertyName];

        return property switch
        {
            OpenApiSchema concrete => concrete.Description ?? string.Empty,
            OpenApiSchemaReference reference => reference.Description ?? string.Empty,
            _ => string.Empty,
        };
    }

    private static IEnumerable<string> AllDescriptions(OpenApiDocument document)
    {
        if (document.Info?.Description is { } info)
        {
            yield return info;
        }

        if (document.Tags is { } tags)
        {
            foreach (var tag in tags)
            {
                if (tag.Description is { } tagDescription)
                {
                    yield return tagDescription;
                }
            }
        }

        if (document.Paths is { } paths)
        {
            foreach (var path in paths.Values)
            {
                if (path is not OpenApiPathItem pathItem || pathItem.Operations is null)
                {
                    continue;
                }

                foreach (var operation in pathItem.Operations.Values)
                {
                    if (operation.Summary is { } summary)
                    {
                        yield return summary;
                    }

                    if (operation.Description is { } description)
                    {
                        yield return description;
                    }

                    foreach (var parameter in operation.Parameters ?? [])
                    {
                        if (parameter is OpenApiParameter concrete && concrete.Description is { } parameterDescription)
                        {
                            yield return parameterDescription;
                        }
                    }
                }
            }
        }

        if (document.Components?.Schemas is not { } schemas)
        {
            yield break;
        }

        foreach (var schema in schemas.Values)
        {
            if (schema is not OpenApiSchema concrete)
            {
                continue;
            }

            if (concrete.Description is { } schemaDescription)
            {
                yield return schemaDescription;
            }

            if (concrete.Properties is not { } properties)
            {
                continue;
            }

            foreach (var property in properties.Values)
            {
                if (property is OpenApiSchema propertySchema && propertySchema.Description is { } propertyDescription)
                {
                    yield return propertyDescription;
                }
            }
        }
    }

    /// <summary>Leaves a reference to the removed property behind, on purpose.</summary>
    private sealed class DanglingReferenceDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Components?.Schemas?.TryGetValue(Model, out var schema) == true
                && schema is OpenApiSchema concrete
                && concrete.Properties?.TryGetValue("always", out var always) == true
                && always is OpenApiSchema alwaysSchema)
            {
                alwaysSchema.Description = "See Gated for the balance.";
            }
        }
    }

    /// <summary>Injects an unclosed gate, which a doc comment cannot contain.</summary>
    private sealed class UnbalancedGateDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Paths?.TryGetValue(Path, out var path) == true
                && path is OpenApiPathItem pathItem
                && pathItem.Operations?.TryGetValue(HttpMethod.Get, out var operation) == true)
            {
                operation.Description = "Always present. <gate flag=\"loyalty\">and this bit is gated";
            }
        }
    }
}
