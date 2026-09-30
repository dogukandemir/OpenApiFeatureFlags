using System.Text;
using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.Swashbuckle;

/// <summary>
/// Resolves <c>&lt;gate flag="X"&gt;…&lt;/gate&gt;</c> fragments inside text that becomes part of the
/// document, so part of a description can be gated by a feature flag.
/// </summary>
/// <remarks>
/// <para>
/// The syntax is deliberately an XML element rather than something invented, because Swashbuckle's
/// XML comment humanizer passes unknown tags through <b>verbatim</b> (measured, see
/// <c>tests/OpenApiFeatureFlags.Swashbuckle.Tests/XmlCommentsHumanizerTests.cs</c>). That means an
/// author can write the gate in a normal XML doc comment and it arrives in
/// <c>OpenApiOperation.Description</c> / <c>OpenApiSchema.Description</c> intact, ready to resolve —
/// no need to reimplement XML comment reading, and no fragile name matching against prose.
/// </para>
/// <para>
/// Semantics, in priority order:
/// </para>
/// <list type="bullet">
///   <item><description>An <b>enabled</b> flag keeps the inner text and drops the wrapper.</description></item>
///   <item><description>A <b>disabled</b> flag (in <see cref="DocumentMode.Remove"/>) drops the inner text too.</description></item>
///   <item><description><see cref="DocumentMode.Annotate"/> and <see cref="DocumentMode.Include"/> remove
///   nothing, so they keep the text and only strip the wrapper. Gating a sentence is a different
///   decision from annotating a document.</description></item>
///   <item><description>A gate with a <b>missing or empty</b> <c>flag</c> attribute cannot be evaluated,
///   so it hides its content and warns — fail closed.</description></item>
///   <item><description>An <b>unbalanced</b> opening gate hides the rest of the string and warns. A typo
///   must not become a leak.</description></item>
/// </list>
/// <para>
/// Nesting composes with AND semantics: the innermost disabled flag wins, so two nested gates hide the
/// text unless both flags are enabled. The wrapper never survives into the published document.
/// </para>
/// </remarks>
internal sealed class DescriptionGateResolver
{
    private const string OpenTag = "<gate";
    private const string CloseTag = "</gate>";
    private const int OpenTagLength = 5;

    /// <summary>
    /// How deep gates may nest before the resolver stops recursing inside them.
    /// </summary>
    /// <remarks>
    /// Not reachable by accident: it takes 33 levels of nested gates in a single description. It
    /// exists so that pathological text degrades into hidden content instead of a stack overflow,
    /// which cannot be caught and takes the whole process down with it.
    /// </remarks>
    private const int MaxNestingDepth = 32;

    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger _logger;

    public DescriptionGateResolver(IDocumentVisibilityPlanner planner, ILogger logger)
    {
        _planner = planner;
        _logger = logger;
    }

    /// <summary>
    /// Resolves every gate in <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text to resolve; may be null or empty.</param>
    /// <returns>The resolved text, or <see langword="null"/> when the text contains no gate.</returns>
    public string? Resolve(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf(OpenTag, StringComparison.Ordinal) < 0)
        {
            return null;
        }

        var rewritten = Rewrite(text, depth: 0);

        // "<gateway>" and friends satisfy the cheap check above without containing a real gate.
        return string.Equals(rewritten, text, StringComparison.Ordinal) ? null : rewritten;
    }

    private string Rewrite(string text, int depth)
    {
        if (depth > MaxNestingDepth)
        {
            // Fail closed rather than guess: content this deep is not something a person wrote.
            Log.GateTooDeep(_logger, MaxNestingDepth, text.Length);
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            var open = FindOpenTag(text, index);

            if (open < 0)
            {
                builder.Append(text, index, text.Length - index);
                break;
            }

            builder.Append(text, index, open - index);

            var tagEnd = text.IndexOf('>', open);

            if (tagEnd < 0)
            {
                // An opening gate that is never even closed: hide the remainder.
                Log.GateUnbalanced(_logger, text[open..]);
                break;
            }

            var tag = text[open..(tagEnd + 1)];

            if (tag.EndsWith("/>", StringComparison.Ordinal))
            {
                // A self-closing gate wraps nothing, so there is nothing to keep or hide.
                index = tagEnd + 1;
                continue;
            }

            var attributes = text[(open + OpenTagLength)..tagEnd];
            var flagName = ReadFlagName(attributes);
            var hides = ShouldHide(flagName, tag);

            var close = FindMatchingClose(text, tagEnd + 1);

            if (close < 0)
            {
                if (!hides)
                {
                    // Keep the remainder, minus the stray wrapper.
                    builder.Append(text, tagEnd + 1, text.Length - tagEnd - 1);
                }

                Log.GateUnbalanced(_logger, tag);
                break;
            }

            var inner = text[(tagEnd + 1)..close];

            if (!hides)
            {
                builder.Append(Rewrite(inner, depth + 1));
            }

            index = close + CloseTag.Length;
        }

        return builder.ToString();
    }

    private bool ShouldHide(string? flagName, string tag)
    {
        if (flagName is null)
        {
            Log.GateWithoutFlag(_logger, tag);
            return true;
        }

        if (_planner.Mode != DocumentMode.Remove)
        {
            return false;
        }

        if (_planner.IsFlagEnabled(flagName))
        {
            return false;
        }

        Log.DescriptionFragmentHidden(_logger, flagName);
        return true;
    }

    /// <summary>Finds the next real <c>&lt;gate</c> opening tag, ignoring things like <c>&lt;gateway&gt;</c>.</summary>
    private static int FindOpenTag(string text, int start)
    {
        var index = start;

        while (index < text.Length)
        {
            var at = text.IndexOf(OpenTag, index, StringComparison.Ordinal);

            if (at < 0)
            {
                return -1;
            }

            var after = at + OpenTagLength;

            if (after < text.Length && (char.IsWhiteSpace(text[after]) || text[after] is '>' or '/'))
            {
                return at;
            }

            index = after;
        }

        return -1;
    }

    private static int FindMatchingClose(string text, int start)
    {
        var depth = 1;
        var index = start;

        while (depth > 0)
        {
            var nextOpen = FindOpenTag(text, index);
            var nextClose = text.IndexOf(CloseTag, index, StringComparison.Ordinal);

            if (nextClose < 0)
            {
                return -1;
            }

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                var tagEnd = text.IndexOf('>', nextOpen);

                if (tagEnd < 0)
                {
                    return -1;
                }

                // A self-closing nested gate closes itself, so it must not deepen the count.
                if (tagEnd == nextOpen || text[tagEnd - 1] != '/')
                {
                    depth++;
                }

                index = tagEnd + 1;
            }
            else
            {
                depth--;

                if (depth == 0)
                {
                    return nextClose;
                }

                index = nextClose + CloseTag.Length;
            }
        }

        return -1;
    }

    private static string? ReadFlagName(string attributes)
    {
        var index = 0;

        while (index < attributes.Length)
        {
            var at = attributes.IndexOf("flag", index, StringComparison.OrdinalIgnoreCase);

            if (at < 0)
            {
                return null;
            }

            var nameIsWhole = at == 0 || char.IsWhiteSpace(attributes[at - 1]);
            var after = at + 4;
            var equals = after;

            while (equals < attributes.Length && char.IsWhiteSpace(attributes[equals]))
            {
                equals++;
            }

            if (!nameIsWhole || equals >= attributes.Length || attributes[equals] != '=')
            {
                index = after;
                continue;
            }

            var valueStart = equals + 1;

            while (valueStart < attributes.Length && char.IsWhiteSpace(attributes[valueStart]))
            {
                valueStart++;
            }

            if (valueStart >= attributes.Length)
            {
                return null;
            }

            var quote = attributes[valueStart];

            if (quote is not ('"' or '\''))
            {
                return null;
            }

            var valueEnd = attributes.IndexOf(quote, valueStart + 1);

            if (valueEnd < 0)
            {
                return null;
            }

            var value = attributes[(valueStart + 1)..valueEnd].Trim();
            return value.Length == 0 ? null : value;
        }

        return null;
    }
}
