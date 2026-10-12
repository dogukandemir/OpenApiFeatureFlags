using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OpenApiFeatureFlags.Analyzers;

/// <summary>Shared inspection of <c>&lt;gate&gt;</c> elements in documentation comments.</summary>
internal static class GateSyntax
{
    private const string GateElementName = "gate";
    private const string FlagAttributeName = "flag";

    /// <summary>
    /// True for a <c>&lt;gate&gt;</c> element. In C#, XML element syntax nodes only occur inside
    /// documentation comments, so no further containment check is needed; the escaped form
    /// (<c>&amp;lt;gate&amp;gt;</c>) arrives as text and never matches.
    /// </summary>
    internal static bool IsGateElement(XmlElementSyntax element) =>
        string.Equals(element.StartTag.Name.LocalName.ValueText, GateElementName, StringComparison.Ordinal);

    internal static bool HasFlagAttribute(XmlElementSyntax element) =>
        element.StartTag.Attributes.Any(
            attribute => string.Equals(attribute.Name.LocalName.ValueText, FlagAttributeName, StringComparison.Ordinal));
}
