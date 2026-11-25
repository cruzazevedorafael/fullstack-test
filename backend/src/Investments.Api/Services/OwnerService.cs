using Investments.Api.Contracts;
using Investments.Api.Data;
using Investments.Domain.Entities;
using Investments.Domain.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Investments.Api.Services;

public sealed class OwnerService(InvestmentsDbContext db)
{
    private const int SqliteUniqueConstraint = 2067;

    public async Task<IReadOnlyList<OwnerResponse>> ListAsync(CancellationToken cancellationToken) =>
        await db.Owners.AsNoTracking()
            .OrderBy(o => o.Name).ThenBy(o => o.Id)
            .Select(o => new OwnerResponse(o.Id, o.Name, o.Email))
            .ToListAsync(cancellationToken);

    public async Task<OwnerResponse> CreateAsync(CreateOwnerRequest request, CancellationToken cancellationToken)
    {
        var owner = Owner.Create(request.Name, request.Email);
        db.Owners.Add(owner);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraint })
        {
            throw new DomainException("owner.email_in_use", "There is already a person with this e-mail.", DomainErrorKind.Conflict);
        }

        return OwnerResponse.From(owner);
    }
}
