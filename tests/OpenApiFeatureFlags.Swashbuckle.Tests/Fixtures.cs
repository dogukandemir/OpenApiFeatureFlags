using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

public sealed class AuditInfo
{
    public string? Actor { get; set; }
}

public sealed class ShopModel
{
    public string? Id { get; set; }

    [OpenApiFeatureFlag("loyalty")]
    public int LoyaltyPoints { get; set; }

    [OpenApiFeatureFlag("loyalty")]
    [Required]
    public string? LoyaltyTier { get; set; }

    /// <summary>Gated property whose type is its own component, so hiding it can orphan a schema.</summary>
    [OpenApiFeatureFlag("audit")]
    public AuditInfo? Audit { get; set; }

    public string? Note { get; set; }
}

[ApiController]
[Route("api/shops")]
public sealed class ShopController : ControllerBase
{
    [HttpGet("{id}")]
    public ShopModel Get(string id) => new() { Id = id };

    [HttpGet("reports")]
    [OpenApiFeatureFlag("reports")]
    public IActionResult Reports() => Ok();

    [HttpGet("search")]
    public IActionResult Search([OpenApiFeatureFlag("internal-search")] string? scope)
    {
        _ = scope;
        return Ok();
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok();
}

[ApiController]
[Route("api/experimental")]
[OpenApiFeatureFlag("experimental-api")]
public sealed class ExperimentalController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();

    [HttpPost]
    public IActionResult Post() => Ok();
}

/// <summary>Model with no gating at all, used for the byte-identical regression test.</summary>
public sealed class PlainModel
{
    public string? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>Controller with no gating at all, used for the byte-identical regression test.</summary>
[ApiController]
[Route("api/plain")]
public sealed class PlainController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        _ = id;
        return Ok();
    }

    [HttpPost]
    public IActionResult Create([FromBody] PlainModel model) => Ok(model);
}
