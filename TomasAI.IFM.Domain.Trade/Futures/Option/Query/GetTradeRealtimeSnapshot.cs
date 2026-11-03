using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradeRealtimeSnapshot
{

    extension(GetTradeRealtimeSnapshotQuery query)
    {
        public async ValueTask ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var cache = context.RealtimeCache?.Find(query.TradeId);
            var snapshot = cache?.Read();
            if (snapshot?.ValueDate != query.ValueDate) snapshot = null;
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<TradeRealtimeSnapshot?>(snapshot)).ConfigureAwait(false);
        }
    }
}
