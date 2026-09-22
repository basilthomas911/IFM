using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetFundsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundsQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetFundsQuery"/>.</summary>
public static class GetFunds
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetFundsQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetFundsAsync(query.PortfolioId, query.State is null ? null : (FundOperatingState)query.State, query.PageSize, query.PageToken, cancellationToken));
}
