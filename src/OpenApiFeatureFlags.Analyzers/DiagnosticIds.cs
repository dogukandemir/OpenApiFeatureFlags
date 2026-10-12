namespace OpenApiFeatureFlags.Analyzers;

/// <summary>
/// Diagnostic identifiers. The four digits read OAFF + area + sequence, where the leading
/// digit names the part of the family the rule belongs to:
/// 1xxx core gating rules that apply to every document engine, 2xxx the built-in
/// Microsoft.AspNetCore.OpenApi engine, 3xxx the NSwag adapter, 4xxx Swashbuckle (reserved),
/// 5xxx FastEndpoints, 6xxx the flag sources (reserved).
/// </summary>
internal static class DiagnosticIds
{
    internal const string GateWithoutFlag = "OAFF1001";
    internal const string MinimalApiParameter = "OAFF2001";
    internal const string NswagEscapeGate = "OAFF3001";
    internal const string FastEndpointsAttribute = "OAFF5001";

    internal static string HelpLink(string id) =>
        $"https://github.com/dogukandemir/OpenApiFeatureFlags/blob/main/docs/analyzers.md#{id.ToLowerInvariant()}";
}
