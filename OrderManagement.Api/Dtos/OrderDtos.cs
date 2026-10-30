using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Dtos;

public record CreateOrderItemDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

public record CreateOrderDto
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; init; }

    [Required, MinLength(1)]
    public List<CreateOrderItemDto> Items { get; init; } = new();
}

public record OrderItemResponse(int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderResponse(
    int Id,
    int CustomerId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    List<OrderItemResponse> Items);
