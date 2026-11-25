using System.Net.Mail;
using Investments.Domain.Exceptions;

namespace Investments.Domain.Entities;

/// <summary>The person who owns investments.</summary>
public sealed class Owner
{
    public const int NameMaxLength = 120;
    public const int EmailMaxLength = 254;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    private Owner()
    {
        // Required by EF Core.
    }

    public static Owner Create(string name, string email)
    {
        name = name?.Trim() ?? string.Empty;
        email = email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (name.Length == 0 || name.Length > NameMaxLength)
        {
            throw new DomainException("owner.invalid_name", $"Name is required and must have up to {NameMaxLength} characters.");
        }

        if (email.Length > EmailMaxLength || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            throw new DomainException("owner.invalid_email", "A valid e-mail address is required.");
        }

        return new Owner { Id = Guid.NewGuid(), Name = name, Email = email };
    }
}
