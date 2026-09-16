using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using GetFundsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundsRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundMandateReadModel>>;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="GetFundsQuery"/>.</summary>
public static class GetFunds
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this GetFundsQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, parameters.Service.GetFundsAsync(query.Parameters.PortfolioId, query.Parameters.State is null ? null : (FundOperatingState)query.Parameters.State, query.Parameters.PageSize, query.Parameters.PageToken, cancellationToken));
}
