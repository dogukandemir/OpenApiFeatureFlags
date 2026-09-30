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
    /// Always documented, but the <c>loyaltyPoints</c> property of its response is not — see
    /// <see cref="Order"/>.
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<Order> Get(string id) =>
        Orders.TryGetValue(id, out var order) ? Ok(order) : NotFound();

    /// <summary>Documented only once the new checkout has been released.</summary>
    [OpenApiFeatureFlag("NewCheckout")]
    [HttpPost("checkout")]
    public IActionResult Checkout() => Ok();

    /// <summary>
    /// Always documented, but the <c>scope</c> parameter only appears once internal search is on.
    /// </summary>
    [HttpGet("search")]
    public ActionResult<string?> Search([OpenApiFeatureFlag("InternalSearch")] string? scope) => Ok(scope);
}
