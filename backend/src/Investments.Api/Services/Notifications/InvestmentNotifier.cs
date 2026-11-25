using System.Globalization;
using Investments.Domain.Entities;

namespace Investments.Api.Services.Notifications;

public interface IInvestmentNotifier
{
    Task InvestmentCreatedAsync(Investment investment, Owner owner);
    Task InvestmentWithdrawnAsync(Investment investment, Owner owner);
}

/// <summary>
/// Notifies the owner after the operation is already saved. A failure to notify is logged
/// and never undoes or fails the operation itself.
/// </summary>
public sealed class InvestmentNotifier(IEmailSender sender, ILogger<InvestmentNotifier> logger) : IInvestmentNotifier
{
    public Task InvestmentCreatedAsync(Investment investment, Owner owner) =>
        SendAsync(owner, "Your investment was created",
            $"Hello {owner.Name}, your investment of {Format(investment.InitialAmount)} dated {investment.CreationDate:yyyy-MM-dd} was created.");

    public Task InvestmentWithdrawnAsync(Investment investment, Owner owner) =>
        SendAsync(owner, "Your investment was withdrawn",
            $"Hello {owner.Name}, your withdrawal dated {investment.Withdrawal!.Date:yyyy-MM-dd} is complete. " +
            $"Net amount: {Format(investment.Withdrawal.NetAmount)} (taxes: {Format(investment.Withdrawal.TaxAmount)}).");

    private async Task SendAsync(Owner owner, string subject, string body)
    {
        try
        {
            await sender.SendAsync(owner.Email, subject, body, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify owner {OwnerId}", owner.Id);
        }
    }

    private static string Format(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
}
