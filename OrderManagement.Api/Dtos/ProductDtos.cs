using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Dtos;

public record CreateProductDto
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }
}
