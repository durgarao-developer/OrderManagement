using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private const string QueueName = "order-created";

    private readonly AppDbContext _db;
    private readonly ServiceBusClient _serviceBus;

    public OrdersController(AppDbContext db, ServiceBusClient serviceBus)
    {
        _db = db;
        _serviceBus = serviceBus;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetAll()
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .ToListAsync();

        return orders.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> Get(int id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order is null ? NotFound() : ToResponse(order);
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderDto dto)
    {
        var customerExists = await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId);
        if (!customerExists)
            return BadRequest($"Customer {dto.CustomerId} does not exist.");

        // Collapse duplicate product lines so stock is checked once per product.
        var requested = dto.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var productIds = requested.Keys.ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Validate existence and stock before changing anything.
        foreach (var (productId, qty) in requested)
        {
            if (!products.TryGetValue(productId, out var product))
                return BadRequest($"Product {productId} does not exist.");
            if (product.StockQuantity < qty)
                return BadRequest($"Insufficient stock for product {productId} ('{product.Name}'): requested {qty}, available {product.StockQuantity}.");
        }

        var order = new Order { CustomerId = dto.CustomerId, Status = OrderStatus.Pending };

        foreach (var (productId, qty) in requested)
        {
            var product = products[productId];
            product.StockQuantity -= qty;
            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = qty,
                UnitPrice = product.Price
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _db.Orders.Add(order);

        // EnableRetryOnFailure is on (for Azure), so the whole transaction must run
        // inside an execution strategy that can retry the entire block as one unit.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        // Order is safely saved. Drop a message on the queue so a background
        // worker (Week 4) can process it later. The API returns without waiting.
        await using (var sender = _serviceBus.CreateSender(QueueName))
        {
            var payload = $"{{\"orderId\":{order.Id},\"customerId\":{order.CustomerId},\"totalAmount\":{order.TotalAmount}}}";
            await sender.SendMessageAsync(new ServiceBusMessage(payload));
        }

        var created = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstAsync(o => o.Id == order.Id);

        return CreatedAtAction(nameof(Get), new { id = order.Id }, ToResponse(created));
    }

    private static OrderResponse ToResponse(Order o) => new(
        o.Id,
        o.CustomerId,
        o.Status.ToString(),
        o.TotalAmount,
        o.CreatedAt,
        o.Items.Select(i => new OrderItemResponse(
            i.ProductId,
            i.Product?.Name ?? string.Empty,
            i.Quantity,
            i.UnitPrice,
            i.UnitPrice * i.Quantity)).ToList());
}
