using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles deletion of a draft portfolio after checking child-fund composition history.</summary>
public static class DeleteDraftPortfolio
{
    /// <summary>Checks every child fund before creating the draft-deletion event.</summary>
    public static async ValueTask<IPortfolioDomainEvent> ExecuteAsync(
        this DeleteDraftPortfolioCommand command,
        PortfolioAggregate aggregate,
        PortfolioId portfolioId,
        IPortfolioEventStore events,
        DateTime now,
        string principal,
        CancellationToken cancellationToken)
    {
        foreach (var fundId in aggregate.FundIds)
        {
            var fund = await events.LoadFundAsync(new PortfolioFundId(portfolioId.Id, fundId), cancellationToken).ConfigureAwait(false);
            if (fund.Orders.Count != 0)
                throw new InvalidOperationException("A Draft Portfolio with composition history cannot be deleted.");
        }
        return aggregate.DeleteDraft(command.CommandId, command.ExpectedVersion, command.Reason, now, principal);
    }
}
