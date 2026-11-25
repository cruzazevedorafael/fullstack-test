using System.ComponentModel.DataAnnotations;
using Investments.Domain.Entities;

namespace Investments.Api.Contracts;

public sealed record CreateOwnerRequest
{
    [Required, MaxLength(Owner.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(Owner.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;
}

public sealed record OwnerResponse(Guid Id, string Name, string Email)
{
    public static OwnerResponse From(Owner owner) => new(owner.Id, owner.Name, owner.Email);
}
