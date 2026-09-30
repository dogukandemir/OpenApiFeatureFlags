using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// Removes every controller except the ones a test asked for, so the byte-identical regression test
/// can generate a document that contains no gated elements at all.
/// </summary>
/// <remarks>
/// Registered after the framework's own <c>ControllerFeatureProvider</c>, which adds every
/// controller in the application parts; this provider then trims that list.
/// </remarks>
internal sealed class SelectedControllersFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly Type[] _allowed;

    public SelectedControllersFeatureProvider(Type[] allowed) => _allowed = allowed;

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        foreach (var controller in feature.Controllers.ToArray())
        {
            if (!_allowed.Contains(controller.AsType()))
            {
                feature.Controllers.Remove(controller);
            }
        }
    }
}
