using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>The model used by the controller surface: three gated members, two plain ones.</summary>
public sealed class Widget
{
    public int Id { get; set; }

    [JsonPropertyName("loyalty_points")]
    [Required]
    [OpenApiFeatureFlag("Loyalty")]
    public int LoyaltyPoints { get; set; }

    [OpenApiFeatureFlag("Secret")]
    public string? InternalNote { get; set; }

    [Description("Price. <gate flag=\"PriceBeta\">Beta pricing formula in use.</gate>")]
    public decimal Price { get; set; }

    public string? Name { get; set; }
}

/// <summary>A model referenced only by a gated operation, for the orphan-pruning test.</summary>
public sealed class HiddenModel
{
    public string? Value { get; set; }
}

/// <summary>The day-to-day controller surface.</summary>
[ApiController]
[Route("api/widgets")]
public sealed class WidgetController : ControllerBase
{
    [HttpGet]
    public string List() => "all";

    [HttpGet("checkout")]
    [OpenApiFeatureFlag("NewCheckout")]
    public string Checkout() => "checkout";

    [HttpGet("search")]
    public string Search([OpenApiFeatureFlag("SearchEnabled")] string? term = null) => "results";

    // The gate fragment is written escaped on purpose: NSwag's documentation reader extracts element
    // text and drops markup, so a literal <gate> tag never survives into the document. The escaped
    // form arrives as plain text, which is the authoring shape docs/nswag.md records.
    /// <summary>Widget docs. &lt;gate flag="BetaDocs"&gt;Beta widget notes.&lt;/gate&gt;</summary>
    [HttpGet("documented")]
    public string Documented() => "documented";

    [HttpPost]
    public Widget Create([FromBody] Widget widget) => widget;

    [HttpGet("hidden")]
    [OpenApiFeatureFlag("HiddenFeature")]
    public HiddenModel Hidden() => new();
}

/// <summary>A controller gated at the class level, with one action carrying a second flag.</summary>
[ApiController]
[Route("api/legacy")]
[OpenApiFeatureFlag("Legacy")]
public sealed class LegacyController : ControllerBase
{
    [HttpGet]
    public string Get() => "legacy";

    [HttpGet("export")]
    [OpenApiFeatureFlag("Export")]
    public string Export() => "export";
}
