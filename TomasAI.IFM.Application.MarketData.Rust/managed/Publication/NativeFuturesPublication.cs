using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Diagnostics;

namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Application event assembly over already calculated native futures
/// state. Preserves the existing quote-batch/changed-trade ordering and actor
/// envelopes; performs no market aggregation, price selection or VWAP arithmetic.
/// Its owner serializes calls and joins all publisher callbacks before disposal.</summary>
public sealed class NativeFuturesPublication : IDisposable
{
    readonly ITickAggregationEventPublisher publisher;
    readonly ITickQuoteBufferPool pool;
    readonly Dictionary<(MarketDataLane,uint),State> states=[];
    readonly Guid generation;
    readonly Func<MarketDataLane,Guid>? sourceGeneration;
    readonly DateOnly date;
    readonly ushort capacity;
    readonly Func<MarketDataLane,Guid,bool> isCurrent;
    readonly ITickLiveRouter? liveRouter;
    int disposed;
    long sourceQuotes,sourceTrades,quoteBatches,quoteItems,tradeEvents,fullFlushes,partialFlushes,publicationFailures,processingFailures,started,completed;
    readonly long[][] laneMetrics=[new long[11],new long[11]];
    void Record(MarketDataLane lane,int metric,long count=1)
    {var index=lane==MarketDataLane.FuturesGlbx?0:lane==MarketDataLane.FuturesXcbf?1:-1;if(index>=0)Interlocked.Add(ref laneMetrics[index][metric],count);}
    sealed class State(TickContractMapping mapping,MarketDataLane lane)
    {
        internal readonly TickContractMapping Mapping=mapping;
        internal readonly MarketDataLane Lane=lane;
        internal Guid Physical;
        internal long Sequence,LastVwap;
        internal ITickQuoteBufferLease? Quotes;
        internal ushort Count;
        internal PendingQuote? Pending;
        internal DateTimeOffset LastObserved;
        internal long SourceAt,AcceptedAt,EventAt,MarketAt,DurableAt,Accepted;
    }
    sealed record PendingQuote(TickDataId Tick,Guid Event,Guid Command,DateTime Timestamp,QuoteEmissionReason Reason);

    /// <summary>Freezes qualified futures mappings, API ValueDate and logical
    /// generation. The current predicate is nonblocking and fences physical reset.
    /// The pool is a bounded application wire-buffer reservation, not a provider.</summary>
    public NativeFuturesPublication(ITickAggregationEventPublisher publisher,ITickQuoteBufferPool pool,DateOnly valueDate,
        Guid generation,IReadOnlyCollection<(MarketDataLane Lane,TickContractMapping Mapping)> mappings,ushort quoteCapacity,
        Func<MarketDataLane,Guid,bool> isCurrent,ITickLiveRouter? liveRouter=null,Func<MarketDataLane,Guid>? sourceGeneration=null)
    {
        this.publisher=publisher??throw new ArgumentNullException(nameof(publisher));this.pool=pool??throw new ArgumentNullException(nameof(pool));
        this.isCurrent=isCurrent??throw new ArgumentNullException(nameof(isCurrent));
        this.liveRouter=liveRouter;
        this.sourceGeneration=sourceGeneration;
        if(valueDate==default||generation==Guid.Empty||quoteCapacity is 0 or >4096||mappings.Count is <1 or >2048)throw new ArgumentException("Qualified futures publication inputs are required.");
        date=valueDate;this.generation=generation;capacity=quoteCapacity;
        foreach(var(lane,mapping)in mappings)
        {
            if(lane is not(MarketDataLane.FuturesGlbx or MarketDataLane.FuturesXcbf)||mapping.AssetTypeId!=AssetTypeId.Futures||mapping.PublisherId==0||mapping.InstrumentId==0
                ||string.IsNullOrWhiteSpace(mapping.ContractId)||!states.TryAdd((lane,mapping.InstrumentId),new(mapping,lane)))throw new ArgumentException("Invalid/duplicate futures publication mapping.",nameof(mappings));
        }
    }
    static Guid Physical(LiveMarketEvent source)
    {
        Span<byte> bytes=stackalloc byte[16];System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes,source.GenerationLow);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..],source.GenerationHigh);return new(bytes);
    }
    static unsafe long Field(LiveMarketEvent source,int index)=>source.Fields[index];
    static byte Flags(LiveMarketEvent source)=>checked((byte)(Field(source,4)>>16));
    static decimal? Price(long value)=>(value==long.MaxValue)?null:value/1_000_000_000m;
    static FuturesTickQuoteData RawQuote(LiveMarketEvent source)=>new(checked((uint)Field(source,0)),Field(source,1),Field(source,2),Flags(source),
        Field(source,5),Price(Field(source,5)),checked((uint)Field(source,7)),checked((uint)Field(source,9)),
        Field(source,6),Price(Field(source,6)),checked((uint)Field(source,8)),checked((uint)Field(source,10)));
    static FuturesTickTradeData RawTrade(LiveMarketEvent source)=>new(checked((uint)Field(source,0)),Field(source,1),Field(source,2),Flags(source),Field(source,5),Field(source,5)/1_000_000_000m,
        checked((uint)Field(source,6)),checked((byte)Field(source,7)),checked((byte)Field(source,8)),checked((byte)Field(source,9)));
    TickDataEntityId Entity(State state)=>new(state.Mapping.ContractId,date,state.Mapping.AssetTypeId);
    Guid Generation(State state)=>sourceGeneration?.Invoke(state.Lane)??generation;
    PendingQuote Pending(State state,QuoteEmissionReason reason,DateTimeOffset observed)=>state.Pending??=new(
        new(state.Mapping.ContractId,date,checked(state.Sequence+1),observed.UtcDateTime),Guid.NewGuid(),Guid.NewGuid(),observed.UtcDateTime,reason);

    async ValueTask FlushAsync(State state,QuoteEmissionReason reason,DateTimeOffset observed,CancellationToken token)
    {
        if(state.Quotes is null||state.Count==0)return;
        var pending=Pending(state,reason,observed);var entity=Entity(state);var lease=state.Quotes;lease.SetCount(state.Count);
        var packet=new FuturesTickQuoteDataChangedEvent{
            Subject=new(ActorType.Realtime,FuturesTickQuoteDataChangedEvent.Actor,FuturesTickQuoteDataChangedEvent.Verb,entity.Format()),
            Id=pending.Event,CommandId=pending.Command,EntityId=entity,AggregateId=entity.Format(),EventSource=nameof(TickAggregationService),ReceivedOn=pending.Timestamp,
            TickDataId=pending.Tick,AssetTypeId=state.Mapping.AssetTypeId,Dataset=state.Mapping.Dataset,DefinitionDate=state.Mapping.DefinitionDate,
            PublisherId=state.Mapping.PublisherId,InstrumentId=state.Mapping.InstrumentId,EmissionReason=pending.Reason,QuoteCount=state.Count,
            QuoteData=new(lease.Buffer,state.Count),SourceDataset=state.Mapping.Dataset,SourceGenerationId=Generation(state)};
        // Successful acceptance transfers lease ownership, exactly as the current
        // publisher contract. On error keep its identity/buffer for owned recovery.
        try{await publisher.PublishAsync(packet,lease,token).ConfigureAwait(false);}
        catch{Interlocked.Increment(ref publicationFailures);Record(state.Lane,7);throw;}
        Interlocked.Increment(ref quoteBatches);Interlocked.Add(ref quoteItems,state.Count);Record(state.Lane,2);Record(state.Lane,3,state.Count);
        if(pending.Reason==QuoteEmissionReason.BufferFull){Interlocked.Increment(ref fullFlushes);Record(state.Lane,5);}else{Interlocked.Increment(ref partialFlushes);Record(state.Lane,6);}
        Interlocked.Exchange(ref state.DurableAt,observed.UtcTicks);
        state.Sequence=pending.Tick.SequenceId;state.Quotes=null;state.Count=0;state.Pending=null;
    }
    async ValueTask MarketAsync(State state,FuturesMarketPriceSnapshot snapshot,FuturesMarketPriceUpdateSource source,DateTimeOffset observed,
        CancellationToken token,FuturesTickTradeDataChangedEvent? trade=null,FuturesVwapSourceCheckpoint? checkpoint=null)
    {
        var entity=Entity(state);
        try{await publisher.PublishAsync(new FuturesMarketPriceUpdatedRealtimeEvent{
            Subject=new(ActorType.Realtime,checkpoint is null?FuturesMarketPriceUpdatedRealtimeEvent.Actor:FuturesVwapSourceCheckpoint.Actor,FuturesMarketPriceUpdatedRealtimeEvent.Verb,entity.Format()),
            Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=entity,AggregateId=entity.Format(),EventSource=nameof(TickAggregationService),ReceivedOn=observed.UtcDateTime,
            Price=snapshot,UpdateSource=source,SourceTrade=trade,VwapCheckpoint=checkpoint,SourceDataset=state.Mapping.Dataset,SourceGenerationId=Generation(state)},token).ConfigureAwait(false);}
        catch{Interlocked.Increment(ref publicationFailures);Record(state.Lane,7);throw;}
        Interlocked.Exchange(ref state.MarketAt,observed.UtcTicks);
        if(trade is not null){Interlocked.Increment(ref tradeEvents);Record(state.Lane,4);Interlocked.Exchange(ref state.DurableAt,observed.UtcTicks);}
    }
    async ValueTask VwapAsync(State state,LivePublication frame,FuturesMarketPriceSnapshot snapshot,DateTimeOffset observed,CancellationToken token)
    {
        var es=state.Mapping.ContractDetails?.Ticker=="ES"||state.Mapping.ContractDetails is null&&state.Mapping.ContractId.StartsWith("ES",StringComparison.Ordinal);
        if(!es||!frame.TryGetVwap(out var checkpoint)||checkpoint is null||!checkpoint.IsReplayComplete
            ||state.LastVwap!=0&&observed.UtcTicks-state.LastVwap<TimeSpan.FromSeconds(2).Ticks)return;
        await MarketAsync(state,snapshot,FuturesMarketPriceUpdateSource.Trade,observed,token,checkpoint:checkpoint).ConfigureAwait(false);state.LastVwap=observed.UtcTicks;
    }
    async ValueTask StatisticsAsync(State state,FuturesSessionStatisticsSnapshot statistics,DateTimeOffset observed,CancellationToken token)
    {
        var entity=new FuturesEodDataId(state.Mapping.ContractId,statistics.ValueDate);
        try{await publisher.PublishAsync(new FuturesSessionStatisticsUpdatedRealtimeEvent{
            Subject=new(ActorType.Realtime,FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,FuturesSessionStatisticsUpdatedRealtimeEvent.Verb,entity.Format()),
            Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=entity,AggregateId=entity.Format(),EventSource=nameof(TickAggregationService),ReceivedOn=observed.UtcDateTime,
            Statistics=statistics,SourceDataset=state.Mapping.Dataset,SourceGenerationId=Generation(state)},token).ConfigureAwait(false);}
        catch{Interlocked.Increment(ref publicationFailures);Record(state.Lane,7);throw;}
    }
    FuturesTickTradeDataChangedEvent Trade(State state,LiveMarketEvent source,DateTimeOffset observed,long sequence)
    {
        var entity=Entity(state);
        return new(){Subject=new(ActorType.Realtime,FuturesTickTradeDataChangedEvent.Actor,FuturesTickTradeDataChangedEvent.Verb,entity.Format()),
            Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=entity,AggregateId=entity.Format(),EventSource=nameof(TickAggregationService),ReceivedOn=observed.UtcDateTime,
            TickDataId=new(state.Mapping.ContractId,date,sequence,observed.UtcDateTime),AssetTypeId=state.Mapping.AssetTypeId,Dataset=state.Mapping.Dataset,
            DefinitionDate=state.Mapping.DefinitionDate,PublisherId=checked((ushort)(Field(source,4)&0xffff)),InstrumentId=source.Instrument,
            TradeData=RawTrade(source),
            SourceDataset=state.Mapping.Dataset,SourceGenerationId=Generation(state)};
    }
    /// <summary>Consumes one copied coherent contribution. Unknown identities
    /// and publisher failures escape to the required application recovery observer.
    /// Obsolete physical records are fenced before publication or buffer mutation.</summary>
    public async ValueTask ProcessAsync(LivePublication frame,CancellationToken token=default)
    {
        Interlocked.Increment(ref started);Record(frame.Source.Lane,9);
        try{await ProcessCoreAsync(frame,token).ConfigureAwait(false);Interlocked.Increment(ref completed);Record(frame.Source.Lane,10);}
        catch{Interlocked.Increment(ref processingFailures);Record(frame.Source.Lane,8);throw;}
    }
    // The pinned DBN publisher registry assigns XCBF.PITCH exchange and
    // off-exchange statistics to 105/106 for the same instrument. Preserve the
    // raw source publisher on trade evidence; reject unrelated dataset sources.
    static bool SamePublisher(MarketDataLane lane,ushort expected,ushort received)=>expected==received
        ||lane==MarketDataLane.FuturesXcbf&&((expected==105&&received==106)||(expected==106&&received==105));
    async ValueTask ProcessCoreAsync(LivePublication frame,CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);token.ThrowIfCancellationRequested();var source=frame.Source;
        if(!states.TryGetValue((source.Lane,source.Instrument),out var state))throw new InvalidDataException("Unqualified native futures publication.");
        var physical=Physical(source);if(physical==Guid.Empty||!isCurrent(source.Lane,physical))return;
        var observed=frame.ObservedAtUtc;var flags=Flags(source);
        if(source.Kind is not(5 or 6)&&!SamePublisher(source.Lane,state.Mapping.PublisherId,checked((ushort)(Field(source,4)&0xffff))))throw new InvalidDataException("Futures source publisher changed.");
        if(state.Physical!=physical)
        {
            if(state.Physical!=Guid.Empty)await FlushAsync(state,QuoteEmissionReason.FeedFaulted,state.LastObserved,token).ConfigureAwait(false);
            state.Physical=physical;state.Sequence=0;state.LastVwap=0;
            Interlocked.Exchange(ref state.AcceptedAt,0);Interlocked.Exchange(ref state.EventAt,0);
            Interlocked.Exchange(ref state.MarketAt,0);Interlocked.Exchange(ref state.DurableAt,0);
        }
        state.LastObserved=observed;
        Interlocked.Exchange(ref state.SourceAt,observed.UtcTicks);
        if(source.Kind==1){Interlocked.Increment(ref sourceQuotes);Record(source.Lane,0);}
        if(source.Kind==2){Interlocked.Increment(ref sourceTrades);Record(source.Lane,1);}
        if(frame.Update is 1 or 2 or 3)
        {
            Interlocked.Exchange(ref state.AcceptedAt,observed.UtcTicks);
            Interlocked.Exchange(ref state.EventAt,DateTimeOffset.UnixEpoch.AddTicks(Field(source,1)/100).UtcTicks);
            Interlocked.Increment(ref state.Accepted);
        }
        if((flags&2)!=0)return; // Replay updates native aggregates, not live marks.
        // The current C# market snapshot carries the qualified mapping's
        // publisher, while individual trade evidence preserves its raw source.
        if(frame.TryGetMarketPrice(state.Mapping.ContractId,state.Mapping.AssetTypeId,out var market))
            market=market with{PublisherId=state.Mapping.PublisherId};
        if(source.Kind==6)
        {
            if(frame.TryGetSessionStatistics(state.Mapping.ContractId,out var replayed))await StatisticsAsync(state,replayed,observed,token).ConfigureAwait(false);
            return;
        }
        if(source.Kind==5)
        {
            if(frame.TryGetVwap(out var checkpoint)&&checkpoint is {EligibleTradeCount:>0})await VwapAsync(state,frame,default,observed,token).ConfigureAwait(false);
            state.LastVwap=0;
            if(frame.TryGetSessionStatistics(state.Mapping.ContractId,out var stats))await StatisticsAsync(state,stats,observed,token).ConfigureAwait(false);return;
        }
        if(frame.Update==1&&source.Kind==1)
        {
            if(state.Mapping.ContractDetails?.Ticker=="VX")await MarketAsync(state,market,FuturesMarketPriceUpdateSource.Quote,observed,token).ConfigureAwait(false);
            if(liveRouter?.IsActive(state.Mapping.ContractId)==true)await liveRouter.RouteAsync(new LiveTickQuoteServiceEvent(Guid.NewGuid(),state.Mapping.ContractId,date,
                state.Mapping.AssetTypeId,state.Mapping.Dataset,state.Mapping.DefinitionDate,checked((ushort)(Field(source,4)&0xffff)),source.Instrument,RawQuote(source)),token).ConfigureAwait(false);
            if(state.Count==capacity)await FlushAsync(state,QuoteEmissionReason.BufferFull,observed,token).ConfigureAwait(false);
            state.Quotes??=await pool.RentAsync(capacity,token).ConfigureAwait(false);
            state.Quotes.Buffer[state.Count++]=RawQuote(source);
            if(state.Count==capacity)await FlushAsync(state,QuoteEmissionReason.BufferFull,observed,token).ConfigureAwait(false);
        }
        else if(source.Kind==2&&frame.Update is 2 or 3)
        {
            await VwapAsync(state,frame,market,observed,token).ConfigureAwait(false);
            if(liveRouter?.IsActive(state.Mapping.ContractId)==true)await liveRouter.RouteAsync(new LiveTickTradeServiceEvent(Guid.NewGuid(),state.Mapping.ContractId,date,
                state.Mapping.AssetTypeId,state.Mapping.Dataset,state.Mapping.DefinitionDate,checked((ushort)(Field(source,4)&0xffff)),source.Instrument,RawTrade(source)),token).ConfigureAwait(false);
            var pending=state.Count>0?Pending(state,QuoteEmissionReason.TradeObserved,observed):null;
            var trade=frame.Update==3?Trade(state,source,observed,checked(state.Sequence+(pending is null?1:2))):null;
            await FlushAsync(state,QuoteEmissionReason.TradeObserved,observed,token).ConfigureAwait(false);
            if(trade is not null){await MarketAsync(state,market,FuturesMarketPriceUpdateSource.Trade,observed,token,trade).ConfigureAwait(false);state.Sequence=trade.TickDataId.SequenceId;}
        }
        else if(source.Kind==4&&frame.Update==4&&frame.TryGetSessionStatistics(state.Mapping.ContractId,out var stats))
            await StatisticsAsync(state,stats,observed,token).ConfigureAwait(false);
    }
    /// <summary>Flushes remaining application quote-wire buffers on an explicit
    /// joined stop/removal. The caller supplies the original deadline token.</summary>
    public async ValueTask FlushAsync(QuoteEmissionReason reason,DateTimeOffset observed,CancellationToken token=default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        foreach(var state in states.Values)await FlushAsync(state,reason,observed,token).ConfigureAwait(false);
    }
    /// <summary>Emergency retirement after the serialized delivery callback has
    /// joined. Drops stale application quote buffers instead of publishing them
    /// into a replacement source. Independent lanes retain their own buffers.</summary>
    internal void RetireLane(MarketDataLane lane)
    {
        foreach(var(pair,state)in states)if(pair.Item1==lane)
        {
            state.Quotes?.Dispose();state.Quotes=null;state.Count=0;state.Pending=null;state.Physical=Guid.Empty;state.Sequence=0;state.LastVwap=0;
            Interlocked.Exchange(ref state.SourceAt,0);Interlocked.Exchange(ref state.AcceptedAt,0);Interlocked.Exchange(ref state.EventAt,0);
            Interlocked.Exchange(ref state.MarketAt,0);Interlocked.Exchange(ref state.DurableAt,0);
        }
    }
    /// <summary>Non-consuming delivery metrics. Source counts describe live
    /// records delivered from the native queue; historical replay is aggregated
    /// natively and intentionally absent. Legacy ring/sequence diagnostics are
    /// not synthesized as evidence of native transport progress.</summary>
    public TickAggregationMetricsSnapshot GetMetrics()=>new(Interlocked.Read(ref sourceQuotes),Interlocked.Read(ref sourceTrades),
        Interlocked.Read(ref quoteBatches),Interlocked.Read(ref quoteItems),Interlocked.Read(ref tradeEvents),Interlocked.Read(ref fullFlushes),
        Interlocked.Read(ref partialFlushes),0,0,0,Interlocked.Read(ref publicationFailures),Interlocked.Read(ref processingFailures),states.Count,
        states.Values.Count(x=>Volatile.Read(ref x.Quotes)is not null)){RecordsStarted=Interlocked.Read(ref started),RecordsCompleted=Interlocked.Read(ref completed)};
    /// <summary>Reads one dataset's delivery counters. Returning the shared totals
    /// for each dataset would duplicate counts and obscure which lane failed.</summary>
    public TickAggregationMetricsSnapshot GetMetrics(MarketDataLane lane)
    {
        var index=lane switch{MarketDataLane.FuturesGlbx=>0,MarketDataLane.FuturesXcbf=>1,_=>throw new ArgumentOutOfRangeException(nameof(lane))};
        long Read(int metric)=>Interlocked.Read(ref laneMetrics[index][metric]);
        var configured=states.Values.Where(x=>x.Lane==lane).ToArray();
        return new(Read(0),Read(1),Read(2),Read(3),Read(4),Read(5),Read(6),0,0,0,Read(7),Read(8),configured.Length,configured.Count(x=>Volatile.Read(ref x.Quotes)is not null))
            {RecordsStarted=Read(9),RecordsCompleted=Read(10)};
    }
    /// <summary>Reports observed input and acknowledged publication separately.
    /// Route activation is supplied by the epoch's ownership registry.</summary>
    public TickAggregationContractStatus GetStatus(string contract,bool running,bool active,DateTimeOffset? activated)
    {
        var state=states.Values.FirstOrDefault(x=>x.Mapping.ContractId==contract);
        if(state is null)return new(contract,AssetTypeId.Futures,running,false,false);
        static DateTimeOffset? At(long ticks)=>ticks==0?null:new(ticks,TimeSpan.Zero);
        return new(contract,AssetTypeId.Futures,running,true,running,active,At(Interlocked.Read(ref state.SourceAt)),
            At(Interlocked.Read(ref state.MarketAt)),At(Interlocked.Read(ref state.DurableAt)),activated,At(Interlocked.Read(ref state.AcceptedAt)),
            At(Interlocked.Read(ref state.EventAt)),Interlocked.Read(ref state.Accepted));
    }
    /// <summary>Releases only buffers still owned by this event assembler.
    /// The application owner MUST join Process/Flush and publisher callbacks first;
    /// successfully transferred leases remain the publisher's responsibility.</summary>
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        foreach(var state in states.Values){state.Quotes?.Dispose();state.Quotes=null;state.Count=0;state.Pending=null;}
    }
}
