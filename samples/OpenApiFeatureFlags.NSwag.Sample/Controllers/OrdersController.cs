using Microsoft.AspNetCore.Mvc;

namespace OpenApiFeatureFlags.NSwag.Sample.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private static readonly Dictionary<string, Order> Orders = new(StringComparer.Ordinal)
    {
        ["1"] = new Order { Id = "1", LoyaltyPoints = 120, Note = "seeded" },
    };

    /// <summary>Always documented.</summary>
    [HttpGet]
    public ActionResult<IEnumerable<Order>> List() => Ok(Orders.Values);

    // The gates below are written escaped on purpose: NSwag's documentation reader drops element
    // markup, so a literal <gate> tag would never reach the document (docs/nswag.md).
    /// <summary>
    /// Always documented. &lt;gate flag="LoyaltyProgram"&gt;Its loyaltyPoints property is documented
    /// as well, because the loyalty programme has been released.&lt;/gate&gt;
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<Order> Get(string id) =>
        Orders.TryGetValue(id, out var order) ? Ok(order) : NotFound();

    /// <summary>Documented only once the new checkout has been released.</summary>
    [OpenApiFeatureFlag("NewCheckout")]
    [HttpPost("checkout")]
    public IActionResult Checkout() => Ok();

    /// <summary>
    /// Always documented. &lt;gate flag="InternalSearch"&gt;The scope parameter appears below
    /// too.&lt;/gate&gt;
    /// </summary>
    [HttpGet("search")]
    public ActionResult<string?> Search([OpenApiFeatureFlag("InternalSearch")] string? scope) => Ok(scope);
}
