using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
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
    private const string InvoiceContainer = "invoices";

    private readonly AppDbContext _db;
    private readonly BlobServiceClient _blobs;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(AppDbContext db, BlobServiceClient blobs, ILogger<OrderProcessor> logger)
    {
        _db = db;
        _blobs = blobs;
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

        // Load the order with its items and customer so we can build the invoice.
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == message.OrderId);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found; skipping.", message.OrderId);
            return;
        }

        // 1. Generate the invoice and save it to Blob Storage.
        var invoiceText = BuildInvoice(order);
        var blobName = $"invoice-{order.Id}.txt";
        await SaveInvoiceAsync(blobName, invoiceText);
        _logger.LogInformation("Invoice {Blob} saved to Blob Storage.", blobName);

        // 2. Mark the order Completed.
        order.Status = OrderStatus.Completed;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} marked Completed.", message.OrderId);
    }

    private static string BuildInvoice(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========== INVOICE ==========");
        sb.AppendLine($"Order #   : {order.Id}");
        sb.AppendLine($"Customer  : {order.Customer?.Name} ({order.Customer?.Email})");
        sb.AppendLine($"Date      : {order.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine("-----------------------------");
        sb.AppendLine("Item               Qty   Price      Total");
        foreach (var item in order.Items)
        {
            var name = (item.Product?.Name ?? "Item").PadRight(18);
            sb.AppendLine($"{name}{item.Quantity,3}  {item.UnitPrice,8:0.00}  {item.UnitPrice * item.Quantity,9:0.00}");
        }
        sb.AppendLine("-----------------------------");
        sb.AppendLine($"TOTAL                       {order.TotalAmount,9:0.00}");
        sb.AppendLine("=============================");
        return sb.ToString();
    }

    private async Task SaveInvoiceAsync(string blobName, string content)
    {
        var container = _blobs.GetBlobContainerClient(InvoiceContainer);
        await container.CreateIfNotExistsAsync();          // make the folder if missing

        var blob = container.GetBlobClient(blobName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true);   // upload the file
    }
}
