using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// Golden-file snapshot of a representative gated document.
/// </summary>
/// <remarks>
/// <para>
/// This is the test that notices a change nobody intended: an extra extension left behind, a property
/// order that moved, a stray <c>required</c> entry. Structural assertions elsewhere say "this property
/// is gone"; this one says "and nothing else changed".
/// </para>
/// <para>
/// The snapshot lives next to the test in <c>Golden/</c> and is copied to the output directory. If it
/// is missing the test writes the current output there and fails with instructions, so refreshing it is
/// a deliberate, reviewable commit rather than a silent pass.
/// </para>
/// </remarks>
public sealed class SwashbuckleGoldenDocumentTests
{
    private const string GoldenFileName = "gated-remove.json";

    [Fact]
    public void GatedDocumentMatchesTheGoldenSnapshot()
    {
        var document = TestSwagger.Build(
            new StubFlagSource().WithEnabled("loyalty"),
            controllers: [typeof(ShopController), typeof(ExperimentalController)]);

        var actual = TestSwagger.ToJson(document);
        var goldenPath = Path.Combine(AppContext.BaseDirectory, "Golden", GoldenFileName);

        if (!File.Exists(goldenPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actual);
            Assert.Fail(
                $"No golden file was found, so one was written to '{goldenPath}'. " +
                $"Review it and copy it to 'tests/OpenApiFeatureFlags.Swashbuckle.Tests/Golden/{GoldenFileName}'.");
        }

        var expected = File.ReadAllText(goldenPath).ReplaceLineEndings("\n");

        actual.ReplaceLineEndings("\n").ShouldBe(expected);
    }
}
