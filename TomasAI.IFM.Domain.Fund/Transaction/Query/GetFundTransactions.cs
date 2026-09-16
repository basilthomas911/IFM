using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Query.Actor;
using TomasAI.IFM.Domain.Fund.Transaction.Query.Extensions;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Transaction.Query;

/// <summary>Handles <see cref="GetFundTransactionsQuery"/>.</summary>
public static class GetFundTransactions
{
    /// <summary>Reads and replies with Fund transactions in the requested date range.</summary>
    public static async ValueTask ExecuteAsync(this GetFundTransactionsQuery query, IFundTransactionQueryContext context, CancellationToken cancellationToken)
    {
        var result = await context.GetFundTransactionsAsync(query, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<FundTransactionReadModel[]>(result));
    }
}