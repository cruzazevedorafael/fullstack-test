using Investments.Api.Contracts;
using Investments.Api.Data;
using Investments.Api.Errors;
using Investments.Api.Services.Notifications;
using Investments.Api.Time;
using Investments.Domain.Calculations;
using Investments.Domain.Entities;
using Investments.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Investments.Api.Services;

public sealed class InvestmentService(
    InvestmentsDbContext db,
    IBusinessCalendar calendar,
    InvestmentRules rules,
    IInvestmentNotifier notifier)
{
    public async Task<PagedResponse<InvestmentResponse>> ListAsync(ListInvestmentsRequest request, CancellationToken cancellationToken)
    {
        var query = db.Investments.AsNoTracking();

        if (request.OwnerId is { } ownerId)
        {
            query = query.Where(i => i.OwnerId == ownerId);
        }

        var total = await query.CountAsync(cancellationToken);
        var investments = await query
            .Include(i => i.Owner)
            .OrderByDescending(i => i.CreationDate).ThenBy(i => i.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var today = calendar.Today;
        var items = investments.Select(i => InvestmentResponse.From(i, today, rules)).ToList();

        return new PagedResponse<InvestmentResponse>(items, request.Page, request.PageSize, total);
    }

    public async Task<InvestmentResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await FindAsync(id, cancellationToken));

    public async Task<InvestmentResponse> CreateAsync(CreateInvestmentRequest request, CancellationToken cancellationToken)
    {
        var owner = await db.Owners.FindAsync([request.OwnerId!.Value], cancellationToken)
            ?? throw new NotFoundException("owner.not_found", "Owner not found.");

        var investment = Investment.Create(owner.Id, request.Amount!.Value, request.CreationDate!.Value, calendar.Today);
        db.Investments.Add(investment);
        await db.SaveChangesAsync(cancellationToken);

        await notifier.InvestmentCreatedAsync(investment, owner);

        return ToResponse(investment);
    }

    public async Task<WithdrawalResponse> QuoteWithdrawalAsync(Guid id, DateOnly date, CancellationToken cancellationToken)
    {
        var investment = await FindAsync(id, cancellationToken);

        return WithdrawalResponse.From(investment.QuoteWithdrawal(date, calendar.Today, rules));
    }

    public async Task<InvestmentResponse> WithdrawAsync(Guid id, DateOnly date, CancellationToken cancellationToken)
    {
        var investment = await FindAsync(id, cancellationToken);
        investment.Withdraw(date, calendar.Today, rules);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request withdrew this investment between our read and our write.
            throw new DomainException("investment.already_withdrawn", "This investment has already been withdrawn.", DomainErrorKind.Conflict);
        }

        await notifier.InvestmentWithdrawnAsync(investment, investment.Owner!);

        return ToResponse(investment);
    }

    private async Task<Investment> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Investments.Include(i => i.Owner).FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("investment.not_found", "Investment not found.");

    private InvestmentResponse ToResponse(Investment investment) =>
        InvestmentResponse.From(investment, calendar.Today, rules);
}
