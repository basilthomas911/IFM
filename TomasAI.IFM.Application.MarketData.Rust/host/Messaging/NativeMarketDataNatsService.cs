using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Threading.Channels;
using NATS.Client.Core;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Preserves the current Host RPC contract with independent observation,
/// control and emergency subscribers. It never launches or addresses child workers.
/// Request deduplication is bounded; capacity failure cannot block the stop lane.</summary>
public sealed class NativeMarketDataNatsService(IMarketDataApi api,MarketDataHostConnection connection,
    ILogger<NativeMarketDataNatsService> logger,TimeProvider clock):BackgroundService
{
    readonly MarketDataApiDispatcher dispatcher = new(api);
    readonly object gate=new();
    const string BulkHistoryPath = "api/" + nameof(IMarketDataApi.LoadHistoricalAsync);
    readonly Channel<NatsMsg<MarketDataHostRequest>> bulkHistory = Channel.CreateBounded<NatsMsg<MarketDataHostRequest>>(
        new BoundedChannelOptions(4) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    readonly record struct Memo(string Path, RequestDigest Digest, Task<MarketDataHostReply> Completion);
    readonly record struct RequestDigest(UInt128 First, UInt128 Second)
    {
        public static RequestDigest Compute(ReadOnlySpan<byte> payload)
        {
            Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(payload, hash);
            return new(BinaryPrimitives.ReadUInt128LittleEndian(hash),
                BinaryPrimitives.ReadUInt128LittleEndian(hash[16..]));
        }
    }
    readonly Dictionary<Guid,Memo> operations=[];
    readonly TaskCompletionSource readyCompletion=new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly Queue<Guid> completed=[];
    int ready;
    /// <summary>All three NATS subscriptions must be established before readiness.</summary>
    public bool IsReady=>Volatile.Read(ref ready)==3;
    /// <summary>Completes when all control subscriptions have been installed.</summary>
    public Task Ready=>readyCompletion.Task;
    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)=>Task.WhenAll(
        ListenAsync(".control",8,stoppingToken),ListenAsync(".stop",2,stoppingToken),ListenAsync(".observe",2,stoppingToken),
        RunBulkHistoryAsync(stoppingToken));
    async Task ListenAsync(string suffix,int concurrency,CancellationToken token)
    {
        var client=await connection.GetClientAsync(token).ConfigureAwait(false);
        await using var subscriber=await client.Connection.SubscribeCoreAsync<MarketDataHostRequest>(connection.Scope+suffix,
            serializer:NatsMessagePackSerializer<MarketDataHostRequest>.Default,cancellationToken:token).ConfigureAwait(false);
        if(Interlocked.Increment(ref ready)==3)readyCompletion.TrySetResult();
        try
        {
            await Parallel.ForEachAsync(subscriber.Msgs.ReadAllAsync(token),new ParallelOptions{MaxDegreeOfParallelism=concurrency,CancellationToken=token},async(message,ct)=>
            {
                if (suffix == ".control" && message.Data?.Path == BulkHistoryPath)
                {
                    // Retain the original subject while moving long history
                    // operations off the control slots used by startup seeds.
                    if (!bulkHistory.Writer.TryWrite(message))
                    {
                        var rejected = new MarketDataHostReply([], "Native Host bulk history capacity exhausted. Retry with the same operation identity.");
                        await message.ReplyAsync(rejected,
                            headers: TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Add(null,
                                TomasAI.IFM.Shared.EventModelActor.MessageOrigins.ForPublication(rejected, clock)),
                            serializer: NatsMessagePackSerializer<MarketDataHostReply>.Default, cancellationToken: ct).ConfigureAwait(false);
                    }
                    return;
                }
                await ReplyAsync(message, suffix == ".stop", ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        finally{Interlocked.Decrement(ref ready);}
    }

    async Task RunBulkHistoryAsync(CancellationToken token)
    {
        await foreach (var message in bulkHistory.Reader.ReadAllAsync(token).ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();
            await ReplyAsync(message, false, token).ConfigureAwait(false);
        }
    }

    async Task ReplyAsync(NatsMsg<MarketDataHostRequest> message, bool emergency, CancellationToken token)
    {
        MarketDataHostReply reply;
        try
        {
            var request = message.Data ?? throw new InvalidDataException("Missing market-data request.");
            TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Restore(request,
                TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Read(message.Headers));
            // Emergency work never waits for ordinary deduplication capacity.
            reply = await (emergency ? DispatchAsync(request, token) : JoinAsync(request, token)).ConfigureAwait(false);
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            logger.LogError(error, "Native Host RPC failed");
            reply = new([], TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth.Redact(error.Message));
        }
        var origin = TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Capture(reply, clock);
        await message.ReplyAsync(reply,
            headers: TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Add(null, origin),
            serializer: NatsMessagePackSerializer<MarketDataHostReply>.Default,
            cancellationToken: token).ConfigureAwait(false);
    }

    Task<MarketDataHostReply> JoinAsync(MarketDataHostRequest request,CancellationToken token)
    {
        TaskCompletionSource<MarketDataHostReply> completion;
        var digest = RequestDigest.Compute(request.Payload);
        lock(gate)
        {
            if(operations.TryGetValue(request.OperationId,out var existing))return existing.Path==request.Path&&existing.Digest==digest?existing.Completion:Task.FromResult(new MarketDataHostReply([],"Market-data operation identity was reused with different input."));
            while(completed.Count>0&&operations.Count>=1024)operations.Remove(completed.Dequeue());
            if(operations.Count>=1024)return Task.FromResult(new MarketDataHostReply([],"Native Host command capacity exhausted."));
            completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
            operations.Add(request.OperationId,new(request.Path,digest,completion.Task));
        }
        // Register ownership before execution: synchronous completions cannot race
        // the deduplication table and create a second paid operation.
        _=CompleteAsync(request,completion,token);
        return completion.Task;
    }
    async Task CompleteAsync(MarketDataHostRequest request,TaskCompletionSource<MarketDataHostReply> completion,CancellationToken token)
    {
        try{completion.TrySetResult(await DispatchAsync(request,token).ConfigureAwait(false));}
        catch(OperationCanceledException)when(token.IsCancellationRequested){completion.TrySetCanceled(token);}
        catch(Exception error){completion.TrySetException(error);}
        finally{lock(gate)completed.Enqueue(request.OperationId);}
    }
    async Task<MarketDataHostReply> DispatchAsync(MarketDataHostRequest request,CancellationToken token)
    {
        if(request.Path=="ready")return new(MarketDataHostConnection.Encode(IsReady),null);
        if(!request.Path.StartsWith("api/",StringComparison.Ordinal))return new([],"The native Host accepts IMarketDataApi operations only.");
        var recovery=MarketDataHostConnection.IsRecoveryRequest(request.Path);
        var remaining=new DateTimeOffset(request.DeadlineUtc,TimeSpan.Zero)-TimeProvider.System.GetUtcNow();
        if(!recovery&&remaining<=TimeSpan.Zero)return new([],"Market-data request deadline expired.");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        if(!recovery)deadline.CancelAfter(remaining);
        try
        {
            var call=MarketDataHostConnection.Decode<MarketDataApiCall>(request.Payload);
            TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(request, call);
            // Reject envelope/signature mismatches before invoking a potentially
            // billable or mutating API operation.
            var operation = request.Path.AsSpan(4);
            var signature = call.Signature.AsSpan();
            if (signature.Length <= operation.Length || signature[operation.Length] != '('
                || !signature.StartsWith(operation, StringComparison.Ordinal))
                throw new InvalidDataException("Market-data operation signature differs from its envelope.");
            var payload=await dispatcher.InvokeAsync(call,deadline.Token).ConfigureAwait(false);
            return new(payload,null);
        }
        catch(Exception error)when(!token.IsCancellationRequested)
        {
            logger.LogError("Native market-data operation failed; Method={Method}; OperationId={Operation}; Detail={Detail}",request.Path,request.OperationId,
                TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth.Redact(error.ToString()));
            return new([],TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth.Redact(error.Message));
        }
    }
}
