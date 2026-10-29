using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Publishes the existing binary Host envelopes directly from native
/// event mapping. Each role/dataset owns its sequence and serialization gate;
/// option persistence cannot hold the futures publication gate. Retained state
/// contains bounded latest values for catch-up, never an event replay backlog.</summary>
public sealed class NativeNatsPublisher(MarketDataHostConnection connection,DateOnly valueDate,
    Func<string,string,Guid> owner,CancellationToken lifetime):ITickAggregationEventPublisher
{
    sealed class Lane{internal readonly SemaphoreSlim Gate=new(1);internal long Sequence;internal long Revision=1;}
    readonly Lane[] lanes=[new(),new(),new()];
    readonly object retainedGate=new();
    readonly Dictionary<(string,DatasetPublicationKind),DatasetPublicationEnvelope> retained=[];
    readonly LatestOptionObservations options=new();
    long retainedBytes;
    int running=1;
    /// <summary>Copied futures current values, ordered by original delivery sequence.</summary>
    public DatasetPublicationEnvelope[] CaptureCurrent(){lock(retainedGate)return retained.Values.OrderBy(x=>x.PublicationSequence).ToArray();}
    /// <summary>Copied latest option observations with original timestamps.</summary>
    public DatasetPublicationEnvelope[] CaptureOptions()=>options.Capture();
    /// <summary>Installs a startup futures manifest revision for a prepared lane.</summary>
    public void SetFuturesManifestRevision(string dataset,long revision)
    {if(revision<1||dataset is not("GLBX.MDP3" or "XCBF.PITCH"))throw new ArgumentOutOfRangeException(nameof(revision));Interlocked.Exchange(ref lanes[dataset=="GLBX.MDP3"?0:1].Revision,revision);}
    /// <summary>Updates an application-acknowledged option manifest revision.
    /// Native source generations and observation timestamps are never rewritten.</summary>
    public void SetOptionManifestRevision(long revision)
    {if(revision<1)throw new ArgumentOutOfRangeException(nameof(revision));Interlocked.Exchange(ref lanes[2].Revision,revision);}
    /// <inheritdoc/>
    public bool IsRunning=>Volatile.Read(ref running)!=0;
    /// <inheritdoc/>
    public ValueTask StartAsync(){lifetime.ThrowIfCancellationRequested();Volatile.Write(ref running,1);return ValueTask.CompletedTask;}
    /// <inheritdoc/>
    public ValueTask StopAsync(){Volatile.Write(ref running,0);return ValueTask.CompletedTask;}
    /// <inheritdoc/>
    public ValueTask DisposeAsync()=>StopAsync();
    async ValueTask ForwardAsync<T>(T value,string role,string dataset,Guid generation,DatasetPublicationKind kind,string? retainedContract=null,CancellationToken token=default)
    {
        if(!IsRunning)throw new InvalidOperationException("Native Host publisher is stopped.");
        if(generation==Guid.Empty||dataset is not("GLBX.MDP3" or "XCBF.PITCH")||role=="Options"&&dataset!="GLBX.MDP3")throw new InvalidDataException("Native publication owner is not qualified.");
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime);
        var lane=lanes[role=="Options"?2:dataset=="GLBX.MDP3"?0:1];
        await lane.Gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            var frame=new DatasetPublicationEnvelope{Dataset=dataset,ValueDate=valueDate,WorkerInstanceId=owner(role,dataset),GenerationId=generation,
                ManifestRevision=Interlocked.Read(ref lane.Revision),PublicationSequence=checked(++lane.Sequence),Kind=kind,Payload=MessagePackSerializer.Serialize(value,cancellationToken:linked.Token)};
            if(retainedContract is not null)
            {
                lock(retainedGate)
                {
                    var key=(retainedContract,kind);
                    if(!retained.ContainsKey(key)&&retained.Count>=128)throw new InvalidOperationException("Native Host current-value capacity exhausted.");
                    if(frame.Payload.Length>1024*1024)throw new InvalidDataException("Native Host retained observation is too large.");
                    var next=retainedBytes-(retained.TryGetValue(key,out var previous)?previous.Payload.Length:0)+frame.Payload.Length;
                    if(next>16*1024*1024)throw new InvalidOperationException("Native Host retained-byte capacity exhausted.");
                    retained[key]=frame;retainedBytes=next;
                }
            }
            options.Retain(frame);
            var client=await connection.GetClientAsync(linked.Token).ConfigureAwait(false);
            await client.PublishAsync(connection.Publications(role,frame.WorkerInstanceId),frame,
                serializer:NatsMessagePackSerializer<DatasetPublicationEnvelope>.Default,cancellationToken:linked.Token).ConfigureAwait(false);
        }
        finally{lane.Gate.Release();}
    }
    /// <inheritdoc/>
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value)=>PublishAsync(value,lifetime);
    /// <inheritdoc/>
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent value,CancellationToken token)=>ForwardAsync(value,"Futures",value.SourceDataset,value.SourceGenerationId,DatasetPublicationKind.MarketPrice,value.EntityId.ContractId,token);
    /// <inheritdoc/>
    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent value)=>PublishAsync(value,lifetime);
    /// <inheritdoc/>
    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent value,CancellationToken token)=>ForwardAsync(value,"Futures",value.SourceDataset,value.SourceGenerationId,DatasetPublicationKind.SessionStatistics,value.EntityId.ContractId,token);
    /// <inheritdoc/>
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value)=>PublishAsync(value,lifetime);
    /// <inheritdoc/>
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent value,CancellationToken token)=>ForwardAsync(value,value.AssetTypeId==AssetTypeId.FuturesOption?"Options":"Futures",value.SourceDataset,value.SourceGenerationId,
        value.OptionMarketPriceObservation?.PriceBasis==OptionMarketPriceBasis.QuoteMidpoint?DatasetPublicationKind.OptionQuoteObservation:DatasetPublicationKind.Trade,token:token);
    /// <inheritdoc/>
    public ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,ITickQuoteBufferLease lease)=>PublishAsync(value,lease,lifetime);
    /// <inheritdoc/>
    public async ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent value,ITickQuoteBufferLease lease,CancellationToken token)
    {try{await ForwardAsync(value,value.AssetTypeId==AssetTypeId.FuturesOption?"Options":"Futures",value.SourceDataset,value.SourceGenerationId,DatasetPublicationKind.Quote,token:token).ConfigureAwait(false);}finally{lease.Dispose();}}
}
