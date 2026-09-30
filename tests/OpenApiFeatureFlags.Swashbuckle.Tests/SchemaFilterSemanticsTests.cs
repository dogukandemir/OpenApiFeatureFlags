using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// Pins the Swashbuckle behaviour the adapter depends on: schema filters run for members, and each
/// member is visited before the type that contains it. If a Swashbuckle upgrade changes either, marker-based property pruning stops working
/// and these tests fail with an explanation rather than a mysterious leaked property.
/// </summary>
public sealed class SchemaFilterSemanticsTests
{
    [Fact]
    public void SchemaFilterIsInvokedForPropertiesWithMemberInfo()
    {
        var observed = new List<string>();

        TestSwagger.Build(
            new StubFlagSource(),
            useFeatureFlagFilters: false,
            configureSwagger: options => options.SchemaFilter<RecordingSchemaFilter>(observed));

        observed.ShouldContain(entry => entry == "member:LoyaltyPoints");
    }

    [Fact]
    public void MembersAreVisitedBeforeTheirParentType()
    {
        var observed = new List<string>();

        TestSwagger.Build(
            new StubFlagSource(),
            useFeatureFlagFilters: false,
            configureSwagger: options => options.SchemaFilter<RecordingSchemaFilter>(observed));

        var memberIndex = observed.IndexOf("member:LoyaltyPoints");
        var parentIndex = observed.IndexOf("type:ShopModel");

        memberIndex.ShouldBeGreaterThanOrEqualTo(0);
        parentIndex.ShouldBeGreaterThanOrEqualTo(0);
        memberIndex.ShouldBeLessThan(parentIndex);
    }

    [Fact]
    public void ParameterSchemasArriveWithParameterInfoAndNoMemberInfo()
    {
        // The adapter deliberately ignores these: removing a parameter is the operation filter's job,
        // because only it can actually take the parameter out of the operation.
        var observed = new List<string>();

        TestSwagger.Build(
            new StubFlagSource(),
            useFeatureFlagFilters: false,
            configureSwagger: options => options.SchemaFilter<RecordingSchemaFilter>(observed));

        observed.ShouldContain(entry => entry.StartsWith("parameter:scope", StringComparison.Ordinal));
    }

    private sealed class RecordingSchemaFilter(List<string> observed) : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.MemberInfo is { } member)
            {
                observed.Add("member:" + member.Name);
            }
            else if (context.ParameterInfo is { } parameter)
            {
                observed.Add("parameter:" + parameter.Name);
            }
            else
            {
                observed.Add("type:" + context.Type.Name);
            }
        }
    }
}
