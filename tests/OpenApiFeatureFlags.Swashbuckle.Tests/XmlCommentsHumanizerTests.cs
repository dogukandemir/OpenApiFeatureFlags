using Shouldly;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// Pins the transport that makes description gating possible without reimplementing XML comment
/// reading: Swashbuckle's humanizer passes tags it does not know through <b>verbatim</b>.
/// </summary>
/// <remarks>
/// If a Swashbuckle upgrade starts stripping or escaping unknown tags, <c>&lt;gate&gt;</c> would stop
/// arriving in <c>OpenApiOperation.Description</c> and the whole feature would silently degrade to
/// "the wrapper leaks and nothing is hidden". These tests are the alarm for that.
/// </remarks>
public sealed class XmlCommentsHumanizerTests
{
    [Theory]
    [InlineData("<gate flag=\"LoyaltyProgram\">gated text</gate> after the gate.")]
    [InlineData("Before. <gate flag=\"A\">one</gate> then <gate flag=\"B\">two</gate>.")]
    [InlineData("<gate flag=\"A\">outer<gate flag=\"B\">inner</gate></gate>")]
    public void UnknownTagsSurviveVerbatim(string input) =>
        XmlCommentsTextHelper.Humanize(input).ShouldBe(input);

    [Theory]
    // A cref becomes the dotted member name as plain text, which is how a dangling reference becomes
    // visible to a reader after the member it names is removed.
    [InlineData("See <see cref=\"Order.LoyaltyPoints\"/> for details.", "See Order.LoyaltyPoints for details.")]
    // Inline and block code keep their content, wrapped in backticks.
    [InlineData("<c>x</c>", "`x`")]
    [InlineData("<code>x</code>", "```x```")]
    public void DocumentsTheTransformationsTheAdapterReliesOn(string input, string expected) =>
        XmlCommentsTextHelper.Humanize(input).ShouldBe(expected);

    [Fact]
    public void ABrBecomesTheEnvironmentNewLine() =>
        XmlCommentsTextHelper.Humanize("one.<br/>two.")
            .ShouldBe("one." + Environment.NewLine + "two.");

    [Fact]
    public void AGateSpanningALineBreakStillArrives() =>
        XmlCommentsTextHelper.Humanize("a<gate flag=\"F\">one<br/>two</gate>b")
            .ShouldBe("a<gate flag=\"F\">one" + Environment.NewLine + "two</gate>b");
}
