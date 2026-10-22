using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;

/// <summary>Actor adapter for Host-owned monitoring; no pricing or provider ownership runs in this actor.</summary>
public sealed class QualifiedIndividualOptionFeeds(TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi api) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public async Task AcquireAsync(TickerStreamOwner owner, FuturesOptionContractReadModel contract, DateOnly valueDate)
        => _ = await api.AcquireMonitoredOptionAsync(owner, contract, valueDate).ConfigureAwait(false);
    public async Task ReleaseAsync(TickerStreamOwner owner, string contractId)
        => _ = await api.ReleaseMonitoredOptionAsync(owner, contractId).ConfigureAwait(false);
    internal static OptionDefinitionCandidate CreateCandidate(FuturesOptionContractReadModel value)
    {
        if (value.PublisherId is null || value.InstrumentId is null || value.ExpirationUtc is null
            || string.IsNullOrWhiteSpace(value.MappingVersion) || string.IsNullOrWhiteSpace(value.DefinitionDigest)
            || string.IsNullOrWhiteSpace(value.RawSymbol) || string.IsNullOrWhiteSpace(value.Dataset)
            || string.IsNullOrWhiteSpace(value.UnderlyingContractId)
            || value.OptionRight is not (ReferenceOptionRight.Call or ReferenceOptionRight.Put))
            throw new InvalidDataException("OptionMonitoring.REFERENCE.INCOMPLETE");
        return new(value.ContractId, value.MappingVersion, value.DefinitionDigest, new OptionContractDefinition
        {
            Dataset = value.Dataset, RawSymbol = value.RawSymbol, Ticker = value.Symbol,
            Underlying = value.UnderlyingContractId, Instrument = new(value.PublisherId.Value, value.InstrumentId.Value),
            Right = value.OptionRight == ReferenceOptionRight.Call ? OptionRightSelection.Call : OptionRightSelection.Put,
            StrikePrice = value.GetExactStrikePrice(), MaturityDate = DateOnly.FromDateTime(value.ExpirationUtc.Value.UtcDateTime),
            ExpirationTimestampNanoseconds = checked((ulong)(value.ExpirationUtc.Value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100UL),
            ContractMultiplier = value.MultiplierValue is null ? null : checked((int)value.MultiplierValue.Value)
        });
    }
}
