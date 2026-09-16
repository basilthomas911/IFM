using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Reference.Query.Extensions;
using TomasAI.IFM.Domain.Reference.Shared.Queries;

namespace TomasAI.IFM.Domain.Reference.Query;

/// <summary>Handles <see cref="GetTradeStrategyFamiliesQuery"/>.</summary>
public static class GetTradeStrategyFamilies
{
    /// <summary>Reads and replies with all configured strategy families.</summary>
    public static async ValueTask ExecuteAsync(this GetTradeStrategyFamiliesQuery query, IReferenceQueryContext context, CancellationToken cancellationToken)
    {
        var result = await context.GetTradeStrategyFamiliesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result);
    }
}