using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Derives bounded read partitions from original typed inputs; never infers contract identities from prices.</summary>
public sealed class OutcomeRawPartitions(int maximumPartitions = 100000)
{
    readonly HashSet<OutcomeRawPartition> partitions = [];
    readonly HashSet<DateOnly> dates = [];
    readonly HashSet<string> knownFutures = new(StringComparer.Ordinal);
    public IReadOnlyList<OutcomeRawPartition> Partitions => partitions.OrderBy(x=>x.Table,StringComparer.Ordinal)
        .ThenBy(x=>x.AssetType).ThenBy(x=>x.ContractId,StringComparer.Ordinal).ThenBy(x=>x.ValueDate,StringComparer.Ordinal).ToArray();
    public void Add(string contract, DateOnly date, AssetTypeId asset)
    {
        if (string.IsNullOrWhiteSpace(contract)||contract.Length>256||date==default||asset is not (AssetTypeId.Futures or AssetTypeId.FuturesOption))
            throw new InvalidDataException("Original raw partition identity is missing or unsupported.");
        dates.Add(date);
        foreach(var table in new[]{"tick_trade_data","tick_quote_data",asset==AssetTypeId.Futures?"futures_tick_data":"futures_option_tick_data"})
            partitions.Add(new(table,(int)asset,contract,date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)));
        if(asset==AssetTypeId.Futures)partitions.Add(new("futures_tick_data_by_time",(int)asset,contract,date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)));
        if (partitions.Count>maximumPartitions)throw new InvalidDataException("Original raw partition inventory exceeds its finite bound.");
    }
    public void AddBaseline(MarketDataReadSnapshot baseline)
    {
        foreach(var manifest in baseline.Manifests)
            foreach(var contract in manifest.Contracts)Add(contract.DomainContractId,manifest.ValueDate,contract.AssetTypeId);
        foreach(var contract in baseline.Contracts)knownFutures.Add(contract.ContractId);
        foreach(var frame in baseline.CurrentValues)AddFrame(frame);
        // Observations can augment coverage but the rotating list is never the sole authority.
        foreach(var frame in baseline.OptionObservations??[])AddFrame(frame);
    }
    public void CompleteBaselineDates()
    {
        foreach(var contract in knownFutures)foreach(var date in dates.ToArray())Add(contract,date,AssetTypeId.Futures);
    }
    public bool AddRecord(RealtimeSessionArchiveRecord record)
    {
        if(record.Kind=="RawFuturesStorage")
        {
            var batch=MessagePackSerializer.Deserialize<RawFuturesStorageBatch>(record.OriginalWire);
            if(batch.Records.Length is <1 or >RawFuturesStorageBatch.MaximumCount)throw new InvalidDataException("Invalid original raw batch.");
            foreach(var item in batch.Records)
                if(item.Kind==1)AddTrade(MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(item.Payload));
                else if(item.Kind==2)AddQuote(item.Payload);
                else throw new InvalidDataException("Unknown original raw storage kind.");
            return true;
        }
        if(record.Kind.StartsWith("NativeSource/",StringComparison.Ordinal)) {AddFrame(NativeSourceCaptureCodec.DecodeFrame(record.OriginalWire.ToArray()));return true;}
        if(record.Kind.StartsWith("NativeTransport/",StringComparison.Ordinal))
        {
            if(record.Kind.EndsWith("/Batch",StringComparison.Ordinal))foreach(var item in NativeSourceCaptureCodec.DecodeBatch(record.OriginalWire.ToArray()).Sources)AddFrame(NativeSourceCaptureCodec.DecodeFrame(item.Wire));
            else if(record.Kind.EndsWith("/Single",StringComparison.Ordinal))AddFrame(NativeSourceCaptureCodec.DecodeFrame(record.OriginalWire.ToArray()));
            else throw new InvalidDataException("Unknown original native transport framing.");
            return true;
        }
        return false;
    }
    public void AddFrame(DatasetPublicationEnvelope frame)
    {
        switch(frame.Kind)
        {
            case DatasetPublicationKind.Trade:
            case DatasetPublicationKind.OptionQuoteObservation:AddTrade(MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(frame.Payload));break;
            case DatasetPublicationKind.Quote:AddQuote(frame.Payload);break;
            case DatasetPublicationKind.MarketPrice:
                var price=MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(frame.Payload).Price;Add(price.ContractId,price.ValueDate,price.AssetTypeId);break;
            case DatasetPublicationKind.TradeReplayBatch:
                var replay=MessagePackSerializer.Deserialize<FuturesTradeReplayBatchRealtimeEvent>(frame.Payload).EntityId;Add(replay.ContractId,replay.ValueDate,replay.AssetTypeId);break;
            case DatasetPublicationKind.SessionStatistics:
                var stats=MessagePackSerializer.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(frame.Payload).Statistics;Add(stats.ContractId,stats.ValueDate,AssetTypeId.Futures);break;
            case DatasetPublicationKind.OptionTradeEvidence:
                var evidence=MessagePackSerializer.Deserialize<OptionTradeEvidence>(frame.Payload).Source;Add(evidence.ContractId,evidence.ValueDate,AssetTypeId.FuturesOption);break;
            default:throw new InvalidDataException("Unknown original native publication kind.");
        }
    }
    void AddTrade(FuturesTickTradeDataChangedEvent trade)
    {
        if(trade.OptionMarketPriceObservation is {} observation)Add(observation.OptionTickData.ContractId,observation.OptionTickData.ValueDate,AssetTypeId.FuturesOption);
        else Add(trade.TickDataId.ContractId,trade.TickDataId.ValueDate,trade.AssetTypeId);
    }
    void AddQuote(ReadOnlyMemory<byte> wire)
    {
        var quote=MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(wire);
        using var segment=quote.QuoteData;
        if(quote.QuoteCount!=segment.Count)throw new InvalidDataException("Original quote segment count differs.");
        Add(quote.TickDataId.ContractId,quote.TickDataId.ValueDate,quote.AssetTypeId);
    }
}

/// <summary>An original contract/date/asset partition, including safe known legacy overcoverage.</summary>
public sealed record OutcomeRawPartition(string Table,int AssetType,string ContractId,string ValueDate);
