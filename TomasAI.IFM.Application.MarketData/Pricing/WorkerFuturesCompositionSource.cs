using System.Collections.Immutable;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Reads the owning worker's futures quotes without any Treasury or option-chain dependency.</summary>
public sealed class WorkerFuturesCompositionSource(IDatabentoLastPriceReaderProvider prices, DateOnly valueDate,
    Guid generation, Func<string, bool> ownsContract) : ICompositionMarketSource
{
    public Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request, string? continuation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.IncludeOptions || request.GenerationId != generation || continuation is not null
            || request.Futures.IsDefaultOrEmpty || request.Futures.Length > request.MaximumContracts)
            throw new CompositionMarketSourceException("InvalidSnapshotRequest");
        var values = ImmutableArray.CreateBuilder<CompositionMarketInstrument>(request.Futures.Length);
        foreach (var definition in request.Futures)
        {
            if (!ownsContract(definition.ContractId)) throw new CompositionMarketSourceException("ContractMetadataUnavailable");
            if (!prices.GetFuturesReader(definition.ContractId, valueDate).TryGetLastQuote(out var quote)
                || quote.BidPrice is null || quote.AskPrice is null) throw new CompositionMarketSourceException("QuoteUnavailable");
            values.Add(new(definition.ContractId, new OptionPricingQuote(definition.ContractId, quote.BidPrice.Value,
                quote.AskPrice.Value, quote.BidSize, quote.AskSize, quote.EventTimestamp, quote.ReceiveTimestamp,
                quote.SourceSequence, generation), null, null, null, null, definition));
        }
        return Task.FromResult(new CompositionMarketPage(PricingSemanticHash.Compute(request.Futures.OrderBy(x => x.ContractId, StringComparer.Ordinal)),
            generation, values.Count, values.MoveToImmutable(), null));
    }
}
