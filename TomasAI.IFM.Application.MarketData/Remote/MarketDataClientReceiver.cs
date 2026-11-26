using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Receives Host publications and reconciles local read views without owning provider connections or workers.</summary>
public sealed class MarketDataClientReceiver(MarketDataHostConnection connection, IMarketDataApi api,
    MarketDataReadMirror mirror, MarketDataRemoteAdmission admissions, ITickAggregationEventPublisher publisher,
    IMarketDataOperationsRecorder recorder, IActorSupervisor supervisor,
    ILogger<MarketDataClientReceiver> logger, Recovery.OptionFeedProcessingHealth? processingHealth = null,
    MarketDataRolloverProjection? rolloverProjection = null, Storage.IRawFuturesEodSink? rawEod = null,IRealtimePublicationCapture? capture=null,IRealtimeGenerationDrain? generationDrain=null,
    IRealtimeSessionRecorder? sessionRecorder=null) : BackgroundService
{
    readonly DatasetWorkerAdmissionRegistry futures = admissions.Futures;
    readonly DatasetWorkerAdmissionRegistry options = admissions.Options;
    readonly Dictionary<string, DatasetWorkerAdmission> activeFutures = new(StringComparer.Ordinal);
    readonly Dictionary<string, DatasetWorkerAdmission> activeOptions = new(StringComparer.Ordinal);
    readonly Dictionary<(string Role,string Dataset,Guid Generation),IDisposable> recoveryAuthorities=[];
    readonly System.Collections.Concurrent.ConcurrentDictionary<(string Role,string Dataset,Guid Worker,Guid Generation),byte> retiredSourceAuthorities=new();
    readonly Dictionary<(string Role,string Dataset,Guid Generation),IDisposable> retiredCoreAuthorities=[];
    int sourceProtocol;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Hosted infrastructure starts before actor registration; never resolve an actor producer early.
        while (!supervisor.IsReady) await Task.Delay(TimeSpan.FromMilliseconds(50), stoppingToken).ConfigureAwait(false);
        await publisher.StartAsync(stoppingToken).ConfigureAwait(false);
        var futureIngress = new DatasetPublicationIngress(futures, publisher, recorder, evidencePersistedByHost: true, rawEod: rawEod,requireDurableCapture:capture is {Enabled:true});
        var optionIngress = new DatasetPublicationIngress(options, publisher, recorder, evidencePersistedByHost: true,requireDurableCapture:capture is {Enabled:true});
        try { await Task.WhenAll(ReceiveAsync(), ReceiveOptionCacheAsync(), ReconcileAsync(),ReceiveConfirmedSourcesAsync()).ConfigureAwait(false); }
        finally
        {
            mirror.Disconnect();
            foreach(var authority in recoveryAuthorities.Values)authority.Dispose();recoveryAuthorities.Clear();
            foreach(var authority in retiredCoreAuthorities.Values)authority.Dispose();retiredCoreAuthorities.Clear();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await publisher.StopAsync(deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
        }

        async Task ReceiveConfirmedSourcesAsync()
        {
            if(capture is not {Enabled:true} && sessionRecorder is not {Enabled:true})return;
            await using var client=await connection.OpenPublicationClientAsync(stoppingToken).ConfigureAwait(false);
            var serializer=new NatsByteArrayMessageSerializer();
            await using var subscription=await client.Connection.SubscribeCoreAsync<byte[]>(connection.Scope+".source-capture.>",serializer:serializer,
                opts:new NatsSubOpts{ChannelOpts=new NatsSubChannelOpts{Capacity=128,FullMode=System.Threading.Channels.BoundedChannelFullMode.Wait}},cancellationToken:stoppingToken).ConfigureAwait(false);
            await client.PingAsync(stoppingToken).ConfigureAwait(false);
            await foreach(var message in subscription.Msgs.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                if(message.Data is not{} wire||string.IsNullOrWhiteSpace(message.ReplyTo))continue;
                if(message.Subject==connection.Scope+".source-capture.hello")
                {
                    var valid=false;
                    try{if(wire.Length>1024)throw new InvalidDataException("Source capability message exceeds bounds.");var hello=MessagePack.MessagePackSerializer.Deserialize<NativeSourceCaptureHello>(wire);valid=hello.Version==2&&hello.Scope==connection.Scope;if(valid)Volatile.Write(ref sourceProtocol,2);}
                    catch(Exception failed)when(!stoppingToken.IsCancellationRequested){logger.LogWarning(failed,"Native source capability negotiation was rejected.");}
                    await message.ReplyAsync(MessagePack.MessagePackSerializer.Serialize(new NativeSourceCaptureHelloReceipt(2,connection.Scope,valid)),serializer:serializer,cancellationToken:stoppingToken).ConfigureAwait(false);continue;
                }
                if(message.Subject==connection.Scope+".source-capture.batch.Futures"||message.Subject==connection.Scope+".source-capture.batch.Options")
                {await ReceiveSourceBatchAsync(message).ConfigureAwait(false);continue;}
                DatasetPublicationEnvelope? frame=null;
                var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(wire));var accepted=false;string error="";
                try
                {
                    if(message.Subject!=connection.Scope+".source-capture.Futures"&&message.Subject!=connection.Scope+".source-capture.Options")throw new InvalidDataException("Unsupported required source route.");
                    if(wire.Length is <1 or >2*1024*1024)throw new InvalidDataException("Required source frame exceeds bounds.");
                    frame=NativeSourceCaptureCodec.DecodeFrame(wire);
                    MessageOrigins.Restore(frame,TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Read(message.Headers));
                    var options=message.Subject.EndsWith(".source-capture.Options",StringComparison.Ordinal);
                    await VerifySourceAuthorityAsync(frame,options).ConfigureAwait(false);
                    if (sessionRecorder is { Enabled: true })
                        await sessionRecorder.RecordAsync(new()
                        {
                            Kind = "NativeTransport/" + (options ? "Options" : "Futures") + "/Single",
                            LogicalSourceKey = (options ? "Options|" : "Futures|") + hash,
                            Subject = message.Subject, OriginalWire = wire, Origin = MessageOrigins.Get(frame),
                            Dataset = frame.Dataset, GenerationId = frame.GenerationId, WorkerInstanceId = frame.WorkerInstanceId,
                            Sequence = frame.PublicationSequence
                        }, stoppingToken).ConfigureAwait(false);
                    var ingress = options ? optionIngress : futureIngress;
                    var live = capture is { Enabled: true }
                        ? await ingress.CaptureSourceAsync(frame,stoppingToken).ConfigureAwait(false)
                        : await NativeSessionCaptureOnlyIngress.RecordThenMirrorAsync(sessionRecorder!, connection.Scope, options,
                            frame, wire, (source,token)=>MirrorCapturedCoreAsync(source,options,ingress,token), stoppingToken).ConfigureAwait(false);
                    if(live&&!options)_=mirror.Accept(frame);
                    accepted=true;
                }
                catch(Exception failed)when(!stoppingToken.IsCancellationRequested)
                {error=failed.GetType().Name;logger.LogWarning(failed,"Required native source capture is pending; original source remains retained; Dataset={Dataset}; Sequence={Sequence}",frame?.Dataset,frame?.PublicationSequence);}
                var receipt=MessagePack.MessagePackSerializer.Serialize(new NativeSourceCaptureReceipt(1,frame?.WorkerInstanceId??Guid.Empty,frame?.GenerationId??Guid.Empty,frame?.PublicationSequence??0,hash,accepted,error));
                await message.ReplyAsync(receipt,serializer:serializer,cancellationToken:stoppingToken).ConfigureAwait(false);
            }
        }

        async ValueTask ReceiveSourceBatchAsync(NatsMsg<byte[]> message)
        {
            NativeSourceCaptureBatch? request=null;DatasetPublicationEnvelope[] frames=[];bool accepted=false;string error="";
            try
            {
                if(message.Data is not{} wire||wire.Length is <1 or >512*1024)throw new InvalidDataException("Required source group exceeds bounded wire size.");
                request=NativeSourceCaptureCodec.DecodeBatch(wire);
                if(request.Version!=2||request.Sources.Length is <1 or >64)throw new InvalidDataException("Invalid required source group protocol.");
                frames=new DatasetPublicationEnvelope[request.Sources.Length];
                for(var index=0;index<frames.Length;index++)
                {
                    var source=request.Sources[index]??throw new InvalidDataException("Missing grouped source frame.");frames[index]=NativeSourceCaptureCodec.DecodeFrame(source.Wire);
                    if(source.OriginJson is { Length: > 2048 })throw new InvalidDataException("Grouped source origin exceeds bounds.");
                    MessageOrigins.Restore(frames[index],source.OriginJson is{} origin ? MessageOriginJson.Deserialize(origin) : null);
                }
                var options=message.Subject.EndsWith(".Options",StringComparison.Ordinal);
                await VerifySourceAuthorityAsync(frames[0],options).ConfigureAwait(false);
                if (sessionRecorder is { Enabled: true })
                    await sessionRecorder.RecordAsync(new()
                    {
                        Kind = "NativeTransport/" + (options ? "Options" : "Futures") + "/Batch",
                        LogicalSourceKey = (options ? "Options|" : "Futures|") + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(message.Data!)),
                        Subject = message.Subject, OriginalWire = message.Data!, Dataset = frames[0].Dataset,
                        GenerationId = frames[0].GenerationId, WorkerInstanceId = frames[0].WorkerInstanceId,
                        Sequence = frames[0].PublicationSequence
                    }, stoppingToken).ConfigureAwait(false);
                var ingress = options ? optionIngress : futureIngress;
                var live = capture is { Enabled: true }
                    ? await ingress.CaptureSourceBatchAsync(frames,stoppingToken).ConfigureAwait(false)
                    : await NativeSessionCaptureOnlyIngress.RecordBatchThenMirrorAsync(sessionRecorder!, connection.Scope, options,
                        frames, request.Sources.Select(source => (ReadOnlyMemory<byte>)source.Wire).ToArray(),
                        (source,token)=>MirrorCapturedCoreAsync(source,options,ingress,token), stoppingToken).ConfigureAwait(false);
                if(!options)for(var index=0;index<frames.Length;index++)if(live[index])_=mirror.Accept(frames[index]);
                accepted=true;
            }
            catch(Exception failed)when(!stoppingToken.IsCancellationRequested)
            {error=failed.GetType().Name;logger.LogWarning(failed,"Required native source group remains retained for exact retry; Count={Count}",request?.Sources?.Length);}
            var receipts=request?.Sources?.Length is >=1 and <=64?new NativeSourceCaptureReceipt[request.Sources.Length]:[];
            for(var index=0;index<receipts.Length;index++)
            {
                var frame=index<frames.Length?frames[index]:null;
                receipts[index]=new(1,frame?.WorkerInstanceId??Guid.Empty,frame?.GenerationId??Guid.Empty,frame?.PublicationSequence??0,
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(request!.Sources[index]?.Wire??[])),accepted,error);
            }
            var response=MessagePack.MessagePackSerializer.Serialize(new NativeSourceCaptureBatchReceipt(2,receipts));
            await message.ReplyAsync(response,serializer:new NatsByteArrayMessageSerializer(),cancellationToken:stoppingToken).ConfigureAwait(false);
        }

        async ValueTask VerifySourceAuthorityAsync(DatasetPublicationEnvelope frame,bool option)
        {
            if(Volatile.Read(ref sourceProtocol)!=2)throw new InvalidOperationException("Native source capture protocol2 has not been negotiated.");
            var registry=option?options:futures;var role=option?"Options":"Futures";
            bool Matches()=>registry.TryGet(frame.Dataset,out var current)&&current.WorkerInstanceId==frame.WorkerInstanceId&&current.GenerationId==frame.GenerationId&&current.ValueDate==frame.ValueDate&&current.ManifestRevision==frame.ManifestRevision;
            if(Matches())return;
            var key=(role,frame.Dataset,frame.WorkerInstanceId,frame.GenerationId);if(retiredSourceAuthorities.ContainsKey(key))return;
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);deadline.CancelAfter(TimeSpan.FromSeconds(3));
            // Once per unknown generation, outside all strategy decisions. Source ownership must not depend on the first source ACK's readiness.
            var snapshot=await api.GetBackendReadSnapshotAsync(deadline.Token).ConfigureAwait(false);
            var current=snapshot.Status.Datasets.SingleOrDefault(value=>value.Role==role&&value.Dataset==frame.Dataset);
            if(current is {Running:true}&&(current.Lifecycle is "Starting" or "Healthy" or "Recovering")&&current.WorkerInstanceId==frame.WorkerInstanceId&&current.GenerationId==frame.GenerationId&&current.ValueDate==frame.ValueDate&&current.ManifestRevision==frame.ManifestRevision)
            {await admissions.WaitForSourceAsync(frame,option,deadline.Token).ConfigureAwait(false);return;}
            if(retiredSourceAuthorities.Count>=4096)throw new IOException("Retired source authority capacity exhausted; source remains retained.");
            retiredSourceAuthorities.TryAdd(key,0);
        }

        async ValueTask<bool> MirrorCapturedCoreAsync(DatasetPublicationEnvelope frame,bool option,DatasetPublicationIngress ingress,CancellationToken token)
        {
            var registry=option?options:futures;
            if(registry.TryGet(frame.Dataset,out var current)&&current==new DatasetWorkerAdmission(frame.Dataset,frame.ValueDate,frame.WorkerInstanceId,frame.GenerationId,frame.ManifestRevision))
                return await NativeSessionCaptureOnlyIngress.MirrorConfirmedAsync(registry,frame,ingress.AcceptAsync,token).ConfigureAwait(false);
            var role=option?"Options":"Futures";
            if(!retiredSourceAuthorities.ContainsKey((role,frame.Dataset,frame.WorkerInstanceId,frame.GenerationId)))
                throw new IOException("Original retired source authority has not been inspected; native ownership remains retained.");
            var key=(role,frame.Dataset,frame.GenerationId);
            // Required Core deliveries may still be queued when the source ownership ACK is sent.
            // Retain this bounded recovery-only admission until receiver shutdown, never as live/read-view authority.
            if(!retiredCoreAuthorities.ContainsKey(key))
            {
                if(retiredCoreAuthorities.Count>=4096)throw new IOException("Retired Core recovery authority capacity exhausted.");
                retiredCoreAuthorities[key]=registry.AllowDurableRecovery(frame.Dataset,frame.GenerationId);
            }
            await ingress.PublishRetainedToCoreAsync(frame,token).ConfigureAwait(false);
            return false;
        }

        async Task ReceiveAsync()
        {
            if(capture is {Enabled:true} || sessionRecorder is {Enabled:true})
            {
                // No unowned Core fallback is permitted in confirmed source mode.
                admissions.MarkPublicationsReady();await Task.Delay(Timeout.InfiniteTimeSpan,stoppingToken).ConfigureAwait(false);return;
            }
            await using var client = await connection.OpenPublicationClientAsync(stoppingToken).ConfigureAwait(false);
            await using var subscription = await client.Connection.SubscribeCoreAsync<DatasetPublicationEnvelope>(connection.Scope + ".publications.>",
                serializer: NatsMessagePackSerializer<DatasetPublicationEnvelope>.Default,
                opts: new NatsSubOpts { ChannelOpts = new NatsSubChannelOpts { Capacity = 32768,
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait } }, cancellationToken: stoppingToken).ConfigureAwait(false);
            await client.PingAsync(stoppingToken).ConfigureAwait(false);
            admissions.MarkPublicationsReady();
            await foreach (var message in subscription.Msgs.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                if (message.Data is not { } frame) continue;
                TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Restore(frame,
                    TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Read(message.Headers));
                var isOption = message.Subject.StartsWith(connection.Scope + ".publications.Options.", StringComparison.Ordinal);
                try
                {
                    if (!isOption)
                    {
                        // Preserve the ordered startup prefix while reconciliation installs its manifest.
                        // This is a source admission barrier, outside all trading decisions.
                        await admissions.WaitForFuturesAsync(frame, stoppingToken).ConfigureAwait(false);
                        // The latest view is updated only after required capture; it must not fence a pending retry.
                    }
                    var accepted = await (isOption ? optionIngress : futureIngress).AcceptAsync(frame, stoppingToken).ConfigureAwait(false);
                    if(accepted&&!isOption)_=mirror.Accept(frame);
                    if (isOption && !accepted && frame.Kind == DatasetPublicationKind.OptionQuoteObservation)
                    {
                        var tick = MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>(frame.Payload).OptionMarketPriceObservation!.OptionTickData;
                        logger.LogDebug("Option observation not admitted; Contract={Contract}; Generation={Generation}; QuoteUtc={QuoteUtc}; RiskUtc={RiskUtc}; PublicationSequence={Sequence}",
                            tick.ContractId, frame.GenerationId, tick.ObservedAtUtc, tick.CalculatedAtUtc, frame.PublicationSequence);
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { }
                catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                {
                    if (isOption) processingHealth?.Report(frame.ValueDate, frame.GenerationId, "ApiOptionIngress", error, stoppingToken);
                    logger.LogWarning(error, "MarketData Host publication could not be admitted; Dataset={Dataset}", frame.Dataset);
                }
            }
        }

        async Task ReceiveOptionCacheAsync()
        {
            var client = await connection.GetClientAsync(stoppingToken).ConfigureAwait(false);
            var assembler = new OptionChainCache.OptionChainPublicationAssembler();
            await foreach (var message in client.Connection.SubscribeAsync<OptionChainCache.OptionChainPublicationFrame>(connection.Scope + ".option-cache",
                serializer: NatsMessagePackSerializer<OptionChainCache.OptionChainPublicationFrame>.Default, cancellationToken: stoppingToken).ConfigureAwait(false))
            {
                if (message.Data is not { } frame || !mirror.NeedsOption(frame.GenerationId, frame.Scope, frame.Version)) continue;
                try
                {
                    if (sessionRecorder is { Enabled: true })
                        await sessionRecorder.RecordAsync(new()
                        {
                            Kind = "OptionCacheFrame", Subject = connection.Scope + ".option-cache",
                            LogicalSourceKey = $"{frame.GenerationId:N}|{frame.Scope}|{frame.Version}|{frame.Offset}|{frame.Digest}",
                            OriginalWire = MarketDataHostConnection.Encode(frame), GenerationId = frame.GenerationId,
                            Sequence = frame.Version
                        }, stoppingToken).ConfigureAwait(false);
                    if (assembler.Accept(frame) is { } publication) mirror.AcceptOption(publication);
                }
                catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                {
                    if (mirror.GetRuntimeStatus().ActiveValueDate is { } date) processingHealth?.Report(date, frame.GenerationId, "OptionCacheIngress", error, stoppingToken);
                    logger.LogWarning(error, "Invalid option-cache publication ignored.");
                }
            }
        }

        async Task ReconcileAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    deadline.CancelAfter(TimeSpan.FromSeconds(3));
                    var snapshot = await api.GetBackendReadSnapshotAsync(deadline.Token).ConfigureAwait(false);
                    if(capture is {Enabled:true})
                    {
                        if(generationDrain is null)throw new InvalidOperationException("Durable source capture requires authoritative generation drain.");
                        var pending=await generationDrain.GetPendingGenerationsAsync(deadline.Token).ConfigureAwait(false);
                        foreach(var group in pending.OrderBy(x=>x.FirstCaptureOrdinal))
                        {
                            foreach(var role in new[]{"Futures","Options"})
                            {
                                var registry=role=="Futures"?futures:options;
                                var key=(role,group.Dataset,group.Generation);
                                if(!recoveryAuthorities.ContainsKey(key))recoveryAuthorities[key]=registry.AllowDurableRecovery(group.Dataset,group.Generation);
                            }
                        }
                        if(Volatile.Read(ref sourceProtocol)!=2||snapshot.Status.Datasets.Any(dataset=>dataset.Running&&dataset.Counters?.GetValueOrDefault("Configured source capture protocol")!=2))
                            throw new InvalidOperationException("Native required-source protocol2 is not configured and negotiated; legacy Core input is refused.");
                        await SyncDurableAsync(futures,activeFutures,snapshot,"Futures",generationDrain,deadline.Token).ConfigureAwait(false);
                        await SyncDurableAsync(options,activeOptions,snapshot,"Options",generationDrain,deadline.Token).ConfigureAwait(false);
                        foreach(var key in recoveryAuthorities.Keys.Where(key=>!pending.Any(group=>group.Dataset==key.Dataset&&group.Generation==key.Generation)).ToArray())
                        {recoveryAuthorities[key].Dispose();recoveryAuthorities.Remove(key);}
                        _=mirror.Apply(snapshot);
                    }
                    else
                    {
                        if(sessionRecorder is {Enabled:true} && (Volatile.Read(ref sourceProtocol)!=2 || snapshot.Status.Datasets.Any(dataset=>dataset.Running&&dataset.Counters?.GetValueOrDefault("Configured source capture protocol")!=2)))
                            throw new InvalidOperationException("Capture-only session recording requires negotiated native source protocol2; no legacy Core input fallback is permitted.");
                        if(sessionRecorder is {Enabled:true})
                        {
                            // Provider readiness can depend on the first source ACK. Authenticate the exact
                            // running source before updating advisory read views; Starting is not trading readiness.
                            Sync(futures,activeFutures,snapshot,"Futures",includeStarting:true);
                            Sync(options,activeOptions,snapshot,"Options",includeStarting:true);
                            _=mirror.Apply(snapshot);
                        }
                        else
                        {
                            if (!mirror.Apply(snapshot)) continue;
                            Sync(futures, activeFutures, snapshot, "Futures");Sync(options, activeOptions, snapshot, "Options");
                        }
                    }
                    admissions.NotifyFuturesChanged();
                    // Live delivery and catch-up share ingress serialization and per-contract ordering.
                    // Repeated snapshots are harmless and never rewrite exchange/calculation timestamps.
                    foreach (var observation in capture is {Enabled:true} || sessionRecorder is {Enabled:true}?Array.Empty<DatasetPublicationEnvelope>():snapshot.OptionObservations??[])
                    {
                        try
                        {
                            if (await optionIngress.AcceptAsync(observation, deadline.Token).ConfigureAwait(false))
                            {
                                var tick = MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>(observation.Payload).OptionMarketPriceObservation!.OptionTickData;
                                logger.LogInformation("Option catch-up delivered; Contract={Contract}; Generation={Generation}; QuoteUtc={QuoteUtc}; RiskUtc={RiskUtc}; PublicationSequence={Sequence}",
                                    tick.ContractId, observation.GenerationId, tick.ObservedAtUtc, tick.CalculatedAtUtc, observation.PublicationSequence);
                            }
                        }
                        catch (Exception error) when (!deadline.IsCancellationRequested)
                        {
                            processingHealth?.Report(observation.ValueDate, observation.GenerationId, "OptionCatchUp", error, stoppingToken);
                            logger.LogWarning(error, "Option catch-up failed; retained value remains available for retry; Generation={Generation}", observation.GenerationId);
                        }
                    }
                    if (rolloverProjection is not null)
                    {
                        using var projectionDeadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        projectionDeadline.CancelAfter(TimeSpan.FromSeconds(5));
                        try { await rolloverProjection.ApplyAsync(snapshot, projectionDeadline.Token).ConfigureAwait(false); }
                        catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                        {
                            // Catalog projection retries independently; a read-model write failure is not a provider outage.
                            logger.LogWarning(error, "Active native futures rollover projection will retry.");
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    foreach (var active in activeOptions.Values) processingHealth?.Report(active.ValueDate, active.GenerationId, "HostSynchronization", error, stoppingToken);
                    mirror.Disconnect();
                    if(capture is not {Enabled:true} && sessionRecorder is not {Enabled:true})
                    {
                        foreach (var entry in activeFutures.Values) futures.Close(entry.Dataset, entry.GenerationId);
                        foreach (var entry in activeOptions.Values) options.Close(entry.Dataset, entry.GenerationId);
                        activeFutures.Clear(); activeOptions.Clear();
                    }
                    // Durable mode retains original handler authority through transient synchronization/drain failures.
                    logger.LogWarning(error, "MarketData Host cache synchronization unavailable; read view is fenced.");
                }
                try { if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) break; }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    static async ValueTask SyncDurableAsync(DatasetWorkerAdmissionRegistry registry,Dictionary<string,DatasetWorkerAdmission> active,
        MarketDataReadSnapshot snapshot,string role,IRealtimeGenerationDrain drain,CancellationToken token)
    {
        var next=snapshot.Status.Datasets.Where(dataset=>dataset.Role==role&&dataset.Running&&(dataset.Lifecycle is "Starting" or "Healthy" or "Recovering")&&dataset.ValueDate is not null&&dataset.WorkerInstanceId!=Guid.Empty&&dataset.GenerationId!=Guid.Empty&&dataset.ManifestRevision>0)
            .ToDictionary(dataset=>dataset.Dataset,dataset=>new DatasetWorkerAdmission(dataset.Dataset,dataset.ValueDate!.Value,dataset.WorkerInstanceId,dataset.GenerationId,dataset.ManifestRevision),StringComparer.Ordinal);
        // During native quiescence/recovery, preserve the original admission until a replacement is actually prepared.
        foreach(var pair in active.ToArray())
        {
            if(!next.TryGetValue(pair.Key,out var replacement)||replacement==pair.Value)continue;
            await registry.SealCaptureAsync(pair.Value,token).ConfigureAwait(false);
            await drain.WaitForDrainAsync(pair.Key,pair.Value.GenerationId,token).ConfigureAwait(false);
            registry.Close(pair.Key,pair.Value.GenerationId);active.Remove(pair.Key);
        }
        foreach(var pair in next)
        {
            if(active.TryGetValue(pair.Key,out var existing)&&existing==pair.Value)continue;
            // On cold start original required generations must drain under recovery-only authority before live promotion.
            foreach(var pending in (await drain.GetPendingGenerationsAsync(token).ConfigureAwait(false)).Where(x=>x.Dataset==pair.Key&&x.Generation!=pair.Value.GenerationId).OrderBy(x=>x.FirstCaptureOrdinal))
                await drain.WaitForDrainAsync(pending.Dataset,pending.Generation,token).ConfigureAwait(false);
            registry.Admit(pair.Value);active[pair.Key]=pair.Value;
        }
    }

    internal static void Sync(DatasetWorkerAdmissionRegistry admissions, Dictionary<string, DatasetWorkerAdmission> active,
        MarketDataReadSnapshot snapshot, string role, bool includeStarting=false)
    {
        var next = snapshot.Status.Datasets.Where(dataset => dataset.Role == role && (dataset.Lifecycle == "Healthy"
                ||includeStarting&&dataset.Running&&(dataset.Lifecycle is "Starting" or "Recovering")) && dataset.ValueDate is not null
                &&dataset.WorkerInstanceId!=Guid.Empty&&dataset.GenerationId!=Guid.Empty&&dataset.ManifestRevision>0)
            .ToDictionary(dataset => dataset.Dataset, dataset => new DatasetWorkerAdmission(dataset.Dataset, dataset.ValueDate!.Value,
                dataset.WorkerInstanceId, dataset.GenerationId, dataset.ManifestRevision), StringComparer.Ordinal);
        foreach (var pair in active)
            if(next.TryGetValue(pair.Key,out var replacement)?replacement!=pair.Value:!includeStarting)
                admissions.Close(pair.Key,pair.Value.GenerationId);
        if(includeStarting)
            foreach(var pair in active)if(!next.ContainsKey(pair.Key))next.Add(pair.Key,pair.Value);
        foreach (var pair in next) admissions.Admit(pair.Value);
        active.Clear(); foreach (var pair in next) active.Add(pair.Key, pair.Value);
    }
}
