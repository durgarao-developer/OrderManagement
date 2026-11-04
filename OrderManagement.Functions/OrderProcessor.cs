using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderManagement.Api.Data;
using OrderManagement.Api.Models;

namespace OrderManagement.Functions;

// The "kitchen": runs automatically whenever a message lands on the
// order-created queue, then processes that order in the background.
public class OrderProcessor
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(AppDbContext db, ILogger<OrderProcessor> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Shape of the message the API sends: {"orderId":1,"customerId":1,"totalAmount":1}
    private record OrderMessage(int OrderId, int CustomerId, decimal TotalAmount);

    [Function(nameof(OrderProcessor))]
    public async Task Run(
        [ServiceBusTrigger("order-created", Connection = "ServiceBus")] string messageBody)
    {
        _logger.LogInformation("Received message: {Body}", messageBody);

        var message = JsonSerializer.Deserialize<OrderMessage>(
            messageBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (message is null)
        {
            _logger.LogWarning("Message could not be parsed; skipping.");
            return;
        }

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == message.OrderId);
        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found; skipping.", message.OrderId);
            return;
        }

        // Real Week 4 work would also generate an invoice PDF + email here.
        order.Status = OrderStatus.Completed;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} marked Completed.", message.OrderId);
    }
}
