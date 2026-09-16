using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Query;

/// <summary>Handles one BrokerAccount detail query.</summary>
public static class GetBrokerAccount
{
    /// <summary>Returns the current immutable account state when available.</summary>
    public static async ValueTask ExecuteAsync(this GetBrokerAccountQuery query,
        IBrokerAccountQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<BrokerAccountDefinition?>(context.Store.Get(query.BrokerAccountId)))
            .ConfigureAwait(false);
    }
}
