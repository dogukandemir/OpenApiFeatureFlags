using System.Reflection;

namespace OpenApiFeatureFlags;

/// <summary>
/// Reads <see cref="OpenApiFeatureFlagAttribute"/> occurrences off reflection objects.
/// </summary>
/// <remarks>
/// Every method returns a de-duplicated list in <b>ordinal order</b>. Reflection does not guarantee
/// a stable attribute order, so ordering is imposed here to keep the generated document and its log
/// lines byte-stable between runs.
/// </remarks>
public static class OpenApiFeatureFlagDiscovery
{
    /// <summary>
    /// Gets the flags declared on a type, for example a controller.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>The flags declared on the type itself, never <see langword="null"/>.</returns>
    public static IReadOnlyList<string> ForType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Collect(type.GetCustomAttributes<OpenApiFeatureFlagAttribute>(inherit: false));
    }

    /// <summary>
    /// Gets the flags declared on a member, for example a model property or an action method.
    /// </summary>
    /// <param name="member">The member to inspect.</param>
    /// <returns>The flags declared on the member, never <see langword="null"/>.</returns>
    public static IReadOnlyList<string> ForMember(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Collect(member.GetCustomAttributes<OpenApiFeatureFlagAttribute>(inherit: false));
    }

    /// <summary>
    /// Gets the flags declared on a parameter.
    /// </summary>
    /// <param name="parameter">The parameter to inspect.</param>
    /// <returns>The flags declared on the parameter, never <see langword="null"/>.</returns>
    public static IReadOnlyList<string> ForParameter(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return Collect(parameter.GetCustomAttributes<OpenApiFeatureFlagAttribute>(inherit: false));
    }

    /// <summary>
    /// Gets every flag that governs an action: the controller-level flags plus the action-level flags.
    /// </summary>
    /// <param name="action">The action method.</param>
    /// <returns>
    /// The combined flag list in ordinal order. An empty list means the action is not gated at all.
    /// </returns>
    /// <remarks>
    /// Only the declaring type is inspected. The attribute is not inherited, so a flag on a base
    /// controller does not leak onto a derived controller's own actions.
    /// </remarks>
    public static IReadOnlyList<string> ForOperation(MethodInfo action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var flags = new SortedSet<string>(StringComparer.Ordinal);

        if (action.DeclaringType is { } declaringType)
        {
            flags.UnionWith(ForType(declaringType));
        }

        flags.UnionWith(ForMember(action));
        return [.. flags];
    }

    private static IReadOnlyList<string> Collect(IEnumerable<OpenApiFeatureFlagAttribute> attributes)
    {
        var flags = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var attribute in attributes)
        {
            flags.Add(attribute.FlagName);
        }

        return [.. flags];
    }
}
