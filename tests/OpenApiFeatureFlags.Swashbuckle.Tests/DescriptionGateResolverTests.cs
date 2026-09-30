using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// Direct tests for the <c>&lt;gate&gt;</c> parser, including the cases that cannot be expressed in a
/// real XML doc comment because the compiler requires well-formed XML.
/// </summary>
public sealed class DescriptionGateResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain text with no gate")]
    [InlineData("a <gateway>is not a gate</gateway> b")]
    public void LeavesTextWithoutAGateAlone(string? text) =>
        NewResolver().Resolve(text).ShouldBeNull();

    [Fact]
    public void EnabledFlagKeepsTheTextAndDropsTheWrapper() =>
        NewResolver("f").Resolve("a<gate flag=\"f\">T</gate>b").ShouldBe("aTb");

    [Fact]
    public void DisabledFlagHidesTheTextAndTheWrapper() =>
        NewResolver().Resolve("a<gate flag=\"f\">T</gate>b").ShouldBe("ab");

    [Fact]
    public void EachGateIsDecidedIndependently() =>
        NewResolver("on").Resolve("a<gate flag=\"on\">1</gate><gate flag=\"off\">2</gate>b").ShouldBe("a1b");

    [Fact]
    public void NestedGatesRequireEveryFlag()
    {
        const string Text = "a<gate flag=\"outer\">O<gate flag=\"inner\">I</gate></gate>b";

        NewResolver("outer", "inner").Resolve(Text).ShouldBe("aOIb");
        NewResolver("outer").Resolve(Text).ShouldBe("aOb");
        NewResolver("inner").Resolve(Text).ShouldBe("ab");
        NewResolver().Resolve(Text).ShouldBe("ab");
    }

    [Fact]
    public void ASelfClosingGateInsideAnotherDoesNotBreakTheDepthScan() =>
        NewResolver("f").Resolve("a<gate flag=\"f\"><gate flag=\"g\"/>T</gate>b").ShouldBe("aTb");

    [Theory]
    [InlineData("<gate>T</gate>")]
    [InlineData("<gate flag=\"\">T</gate>")]
    [InlineData("<gate flag=''>T</gate>")]
    public void AGateThatCannotBeEvaluatedHidesItsContent(string gate) =>
        NewResolver().Resolve("a" + gate + "b").ShouldBe("ab");

    [Fact]
    public void SelfClosingGateIsRemovedWhicheverWayTheFlagGoes()
    {
        NewResolver("f").Resolve("a<gate flag=\"f\"/>b").ShouldBe("ab");
        NewResolver().Resolve("a<gate flag=\"f\"/>b").ShouldBe("ab");
    }

    [Fact]
    public void AnUnclosedGateHidesTheRestOfTheText()
    {
        // A typo must not become a leak (D5).
        NewResolver().Resolve("a<gate flag=\"f\">T and more").ShouldBe("a");
        NewResolver("f").Resolve("a<gate flag=\"f\">T and more").ShouldBe("aT and more");
    }

    [Fact]
    public void SingleQuotedAttributesWork() =>
        NewResolver("f").Resolve("a<gate flag='f'>T</gate>b").ShouldBe("aTb");

    [Fact]
    public void ExtraWhitespaceInTheAttributeWorks() =>
        NewResolver("f").Resolve("a<gate   flag = \"f\" >T</gate>b").ShouldBe("aTb");

    [Fact]
    public void AnnotateModeKeepsGatedTextBecauseItHidesNothing() =>
        NewResolver(DocumentMode.Annotate).Resolve("a<gate flag=\"f\">T</gate>b").ShouldBe("aTb");

    [Fact]
    public void IncludeModeKeepsGatedTextBecauseItIgnoresFlags() =>
        NewResolver(DocumentMode.Include).Resolve("a<gate flag=\"f\">T</gate>b").ShouldBe("aTb");

    [Fact]
    public void TextThatIsNotGatedAtAllIsReturnedUnchanged()
    {
        const string Text = "a<gate flag=\"f\">T</gate>b<c>code</c>";

        NewResolver("f").Resolve(Text).ShouldBe("aTb<c>code</c>");
    }

    [Fact]
    public void GatesNestedDeeperThanTheGuardLoseTheirContentInsteadOfOverflowingTheStack()
    {
        // The resolver recurses once per nesting level, and a stack overflow cannot be caught: it
        // takes the process down. Nobody writes 33 nested gates, so the guard only ever fires on
        // pathological text, and what it does when it fires is fail closed.
        NewResolver("f").Resolve("a" + Nest(5, "kept") + "b").ShouldBe("akeptb");

        NewResolver("f").Resolve("a" + Nest(40, "secret") + "b").ShouldBe("ab");
    }

    private static string Nest(int depth, string content) =>
        string.Concat(Enumerable.Repeat("<gate flag=\"f\">", depth))
        + content
        + string.Concat(Enumerable.Repeat("</gate>", depth));

    private static DescriptionGateResolver NewResolver(params string[] enabledFlags) =>
        NewResolver(DocumentMode.Remove, enabledFlags);

    private static DescriptionGateResolver NewResolver(DocumentMode mode, params string[] enabledFlags)
    {
        var planner = new DocumentVisibilityPlanner(
            new StubFlagSource().WithEnabled(enabledFlags),
            Options.Create(new OpenApiFeatureFlagsOptions { Mode = mode }),
            NullLogger<DocumentVisibilityPlanner>.Instance);

        return new DescriptionGateResolver(planner, NullLogger.Instance);
    }
}
