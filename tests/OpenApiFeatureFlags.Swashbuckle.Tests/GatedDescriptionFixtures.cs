using Microsoft.AspNetCore.Mvc;

namespace OpenApiFeatureFlags.Swashbuckle.Tests;

/// <summary>
/// Fixtures whose XML doc comments contain <c>&lt;gate&gt;</c> fragments. The comments must be
/// well-formed XML or the compiler rejects them (CS1570), which is why the unbalanced case is
/// exercised through a consumer filter instead — see <c>DescriptionGateTests</c>.
/// </summary>
public sealed class GatedDescriptionModel
{
    /// <summary>Always documented.</summary>
    public string? Always { get; set; }

    /// <summary>Balance: <gate flag="loyalty">loyalty points are included.</gate></summary>
    public string? Balance { get; set; }

    /// <summary>Reads <gate flag="loyalty"><c>Gated</c> for the balance</gate>.</summary>
    public string? Referring { get; set; }

    /// <summary>Gated away entirely.</summary>
    [OpenApiFeatureFlag("loyalty")]
    public int Gated { get; set; }
}

[ApiController]
[Route("api/descriptions")]
public sealed class GatedDescriptionController : ControllerBase
{
    /// <summary>
    /// Returns the model.
    /// <gate flag="loyalty">Loyalty details are included.</gate>
    /// </summary>
    [HttpGet]
    public ActionResult<GatedDescriptionModel> Get() => Ok(new GatedDescriptionModel());

    /// <summary>Nested: <gate flag="outer">outer<gate flag="inner">-inner</gate></gate>.</summary>
    [HttpGet("nested")]
    public IActionResult Nested() => Ok();

    /// <summary>Broken: <gate>no flag attribute here</gate>.</summary>
    [HttpGet("broken")]
    public IActionResult Broken() => Ok();
}
