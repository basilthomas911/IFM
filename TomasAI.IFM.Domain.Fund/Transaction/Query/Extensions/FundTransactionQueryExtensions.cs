using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Transaction.Query.Extensions;

/// <summary>
/// Provides Fund transaction services and query operations for typed query contexts.
/// </summary>
public static class FundTransactionQueryExtensions
{
    extension(IQueryActorContext<FundTransactionQueryActor> context)
    {
        /// <summary>Gets the Fund database-context factory.</summary>
        public IDbContextFactory DbFactory => GetContext(context).DbFactory;

        /// <summary>Gets the query actor logger.</summary>
        public ILogger<FundTransactionQueryActor> Logger => GetContext(context).Logger;
    }

    extension(IFundTransactionQueryContext context)
    {
        /// <summary>Gets Fund transactions for the query's Fund and date range.</summary>
        /// <param name="query">The Fund transaction query.</param>
        /// <param name="cancellationToken">The token used to cancel database access.</param>
        /// <returns>The matching Fund transaction read models.</returns>
        public async ValueTask<FundTransactionReadModel[]> GetFundTransactionsAsync(
            GetFundTransactionsQuery query,
            CancellationToken cancellationToken = default)
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(query);
            var db = context.DbFactory.FundDb;
            return cancellationToken.CanBeCanceled
                ? [.. await db.GetFundTransactionsAsync(
                    query.FundId,
                    query.StartDate,
                    query.EndDate,
                    cancellationToken).ConfigureAwait(false)]
                : [.. await db.GetFundTransactionsAsync(
                    query.FundId,
                    query.StartDate,
                    query.EndDate).ConfigureAwait(false)];
        }
    }

    static IFundTransactionQueryContext GetContext(
        IQueryActorContext<FundTransactionQueryActor> context)
        => IsArgumentNull.Set(context as IFundTransactionQueryContext, nameof(context))!;
}
