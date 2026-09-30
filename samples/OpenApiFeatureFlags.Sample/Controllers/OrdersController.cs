using Microsoft.AspNetCore.Mvc;

namespace OpenApiFeatureFlags.Sample.Controllers;

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

    /// <summary>
    /// Always documented.
    /// <gate flag="LoyaltyProgram">
    /// Its <c>loyaltyPoints</c> property is documented as well, because the loyalty programme has
    /// been released.
    /// </gate>
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<Order> Get(string id) =>
        Orders.TryGetValue(id, out var order) ? Ok(order) : NotFound();

    /// <summary>Documented only once the new checkout has been released.</summary>
    [OpenApiFeatureFlag("NewCheckout")]
    [HttpPost("checkout")]
    public IActionResult Checkout() => Ok();

    /// <summary>
    /// Always documented.
    /// <gate flag="InternalSearch">The <c>scope</c> parameter appears below too.</gate>
    /// </summary>
    [HttpGet("search")]
    public ActionResult<string?> Search([OpenApiFeatureFlag("InternalSearch")] string? scope) => Ok(scope);
}
