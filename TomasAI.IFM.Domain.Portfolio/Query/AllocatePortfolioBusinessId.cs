using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Query.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using AllocatePortfolioBusinessIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.AllocatePortfolioBusinessIdRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioBusinessIdAllocation>;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Handles <see cref="AllocatePortfolioBusinessIdQuery"/>.</summary>
public static class AllocatePortfolioBusinessId
{
    /// <summary>Executes the mapped Portfolio query and replies with its typed result.</summary>
    public static ValueTask ExecuteAsync(this AllocatePortfolioBusinessIdQuery query, IQueryActorContext<PortfolioQueryActor> context, PortfolioQueryParameters parameters, CancellationToken cancellationToken)
        => PortfolioQueryHandlerModel.ReplyAsync(context, query, PortfolioQueryHandlerModel.AllocateAsync(query, parameters.IdentityAllocator, cancellationToken));
}
