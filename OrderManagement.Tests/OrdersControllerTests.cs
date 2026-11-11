using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Controllers;
using OrderManagement.Api.Data;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Models;

namespace OrderManagement.Tests;

// Unit tests for OrdersController.
// We use EF Core's In-Memory database so NO real Azure SQL is touched.
// Each test gets its own fresh in-memory database (unique name) so tests
// never interfere with each other.
public class OrdersControllerTests
{
    // Helper: build a fresh in-memory database for one test.
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // Helper: build the controller. ServiceBus is null because Get/GetAll
    // never use it (only Create does).
    private static OrdersController CreateController(AppDbContext db) =>
        new OrdersController(db, serviceBus: null!);

    // ---------- Tests for GET /api/orders/{id} ----------

    [Fact]
    public async Task Get_ExistingId_ReturnsOrder()
    {
        // Arrange: put one known order in the fake database
        using var db = CreateInMemoryDb();
        db.Orders.Add(new Order
        {
            Id = 1,
            CustomerId = 5,
            Status = OrderStatus.Completed,
            TotalAmount = 300m
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        // Act: ask for order 1 (fixed value)
        var result = await controller.Get(1);

        // Assert: we expect the order back, with the right values
        var order = Assert.IsType<OrderResponse>(result.Value);
        Assert.Equal(1, order.Id);
        Assert.Equal(5, order.CustomerId);
        Assert.Equal("Completed", order.Status);
        Assert.Equal(300m, order.TotalAmount);
    }

    [Fact]
    public async Task Get_MissingId_ReturnsNotFound()
    {
        // Arrange: empty database (no orders at all)
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        // Act: ask for an order that does not exist
        var result = await controller.Get(999);

        // Assert: the logic should return NotFound (404)
        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Test for GET /api/orders ----------

    [Fact]
    public async Task GetAll_ReturnsAllOrders()
    {
        // Arrange: put two orders in the fake database
        using var db = CreateInMemoryDb();
        db.Orders.Add(new Order { Id = 1, CustomerId = 1, Status = OrderStatus.Pending, TotalAmount = 100m });
        db.Orders.Add(new Order { Id = 2, CustomerId = 2, Status = OrderStatus.Completed, TotalAmount = 250m });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        // Act
        var result = await controller.GetAll();

        // Assert: both orders should come back
        var orders = Assert.IsType<List<OrderResponse>>(result.Value);
        Assert.Equal(2, orders.Count);
    }

    [Fact]
    public async Task GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange: no orders
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        // Act
        var result = await controller.GetAll();

        // Assert: an empty list, not null
        var orders = Assert.IsType<List<OrderResponse>>(result.Value);
        Assert.Empty(orders);
    }
}
