using System.Diagnostics.CodeAnalysis;

namespace OpenApiFeatureFlags;

/// <summary>
/// The kind of document element a visibility decision applies to.
/// </summary>
public enum DocumentElementKind
{
    /// <summary>An operation: one HTTP method on one path.</summary>
    Operation = 0,

    /// <summary>A property of a schema in the document.</summary>
    Property = 1,

    /// <summary>A parameter of an operation.</summary>
    Parameter = 2,
}

/// <summary>
/// Identifies one operation in a generated document.
/// </summary>
/// <remarks>
/// The path is the <b>template exactly as the document engine emits it</b>, including the
/// leading slash and any placeholders (for example <c>/api/orders/{id}</c>). The HTTP method
/// is normalised to upper case so engines that report <c>get</c> and <c>GET</c> agree.
/// </remarks>
public readonly record struct OperationKey
{
    /// <summary>
    /// Initialises a new instance of the <see cref="OperationKey"/> struct.
    /// </summary>
    /// <param name="method">The HTTP method, for example <c>GET</c>.</param>
    /// <param name="pathTemplate">The path template as emitted by the document engine.</param>
    /// <exception cref="ArgumentException">Either argument is null, empty or whitespace.</exception>
    public OperationKey(string method, string pathTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathTemplate);
        Method = method.ToUpperInvariant();
        PathTemplate = pathTemplate;
    }

    /// <summary>Gets the upper-cased HTTP method.</summary>
    public string Method { get; }

    /// <summary>Gets the path template as emitted by the document engine.</summary>
    public string PathTemplate { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Method} {PathTemplate}";
}

/// <summary>
/// Identifies a schema member — a property — by its CLR origin.
/// </summary>
/// <remarks>
/// Keying on the CLR type and member name rather than on the emitted JSON name is what makes
/// the plan engine-agnostic: serializers rename properties (camel-casing,
/// <c>[JsonPropertyName]</c>) and the core must not have to reproduce those rules.
/// </remarks>
public sealed record MemberKey
{
    /// <summary>
    /// Initialises a new instance of the <see cref="MemberKey"/> record.
    /// </summary>
    /// <param name="declaringType">The type that declares the member.</param>
    /// <param name="memberName">The CLR name of the member.</param>
    /// <exception cref="ArgumentNullException"><paramref name="declaringType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="memberName"/> is null, empty or whitespace.</exception>
    public MemberKey(Type declaringType, string memberName)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        DeclaringType = declaringType;
        MemberName = memberName;
    }

    /// <summary>Gets the type that declares the member.</summary>
    public Type DeclaringType { get; }

    /// <summary>Gets the CLR name of the member.</summary>
    public string MemberName { get; }

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider",
        Justification = "Type.FullName and a member name are culture-invariant identifiers.")]
    public override string ToString() => $"{DeclaringType.FullName}.{MemberName}";
}

/// <summary>
/// Identifies one parameter of one action in a generated document.
/// </summary>
/// <remarks>
/// <para>
/// A parameter is <b>not</b> a member of its declaring type: it belongs to a method. Keying one by type
/// and name alone collides as soon as two actions on one controller declare a parameter of the same
/// name — <c>id</c>, <c>scope</c>, <c>request</c> — and a collision here is not cosmetic.
/// <see cref="DocumentVisibilityPlan"/> aggregates its decisions by key, so the two elements merge into
/// one: the plan under-counts, and a query about either of them answers for the other.
/// </para>
/// <para>
/// Two overloads of the same action that each declare a parameter of the same name would still
/// collide. ASP.NET Core does not route those to distinct endpoints in the ordinary case, so this is
/// recorded rather than defended against by putting a full signature in the key.
/// </para>
/// </remarks>
public sealed record ParameterKey
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ParameterKey"/> record.
    /// </summary>
    /// <param name="declaringType">The type that declares the action.</param>
    /// <param name="actionName">The CLR name of the action the parameter belongs to.</param>
    /// <param name="parameterName">The CLR name of the parameter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="declaringType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A name is null, empty or whitespace.</exception>
    public ParameterKey(Type declaringType, string actionName, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        DeclaringType = declaringType;
        ActionName = actionName;
        ParameterName = parameterName;
    }

    /// <summary>Gets the type that declares the action.</summary>
    public Type DeclaringType { get; }

    /// <summary>Gets the CLR name of the action the parameter belongs to.</summary>
    public string ActionName { get; }

    /// <summary>Gets the CLR name of the parameter.</summary>
    public string ParameterName { get; }

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider",
        Justification = "Type.FullName and CLR names are culture-invariant identifiers.")]
    public override string ToString() => $"{DeclaringType.FullName}.{ActionName}({ParameterName})";
}
