using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Query.Model;

/// <summary>Provides shared reply and identity-allocation behavior for Portfolio query handlers.</summary>
internal static class PortfolioQueryHandlerModel
{
    /// <summary>Awaits a typed service result and sends it to the query caller.</summary>
    internal static async ValueTask ReplyAsync<TResult>(IQueryActorContext<PortfolioQueryActor> context, IQuery query, Task<ServiceResult<TResult>> resultTask)
        where TResult : class
    {
        var result = await resultTask.ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
    }

    /// <summary>Allocates a business identifier of the requested Portfolio identity kind.</summary>
    internal static async Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateAsync(
        PortfolioQuery<AllocatePortfolioBusinessIdRequest, PortfolioBusinessIdAllocation> query,
        IPortfolioBusinessIdAllocator allocator,
        CancellationToken cancellationToken)
    {
        var value = query.Parameters.Kind switch
        {
            PortfolioBusinessIdentityKind.Portfolio => (await allocator.AllocatePortfolioIdAsync(cancellationToken).ConfigureAwait(false)).Id,
            PortfolioBusinessIdentityKind.Fund => await allocator.AllocateFundIdAsync(cancellationToken).ConfigureAwait(false),
            PortfolioBusinessIdentityKind.Order => await allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false),
            PortfolioBusinessIdentityKind.Trade => await allocator.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false),
            PortfolioBusinessIdentityKind.Policy => await allocator.AllocatePolicyIdAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(query), "A supported business identity kind is required."),
        };
        return new ServiceOk<PortfolioBusinessIdAllocation>(new() { Kind = query.Parameters.Kind, Value = value, CorrelationId = query.CorrelationId });
    }
}