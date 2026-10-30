using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Dtos;

public record CreateCustomerDto
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;
}
