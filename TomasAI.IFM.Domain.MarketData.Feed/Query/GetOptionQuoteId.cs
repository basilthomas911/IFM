using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetOptionQuoteId
{
    internal static async ValueTask<ScalarValue<int>> GetOptionQuoteIdAsync(
        this GetOptionQuoteIdQuery q,
        ISequenceIdGenerator sequenceIdGenerator,
        CancellationToken cancellationToken = default)
        => new(checked((int)await sequenceIdGenerator
            .GetSequenceIdAsync(SequenceName.OptionQuote_QuoteId, cancellationToken)
            .ConfigureAwait(false)));
}
