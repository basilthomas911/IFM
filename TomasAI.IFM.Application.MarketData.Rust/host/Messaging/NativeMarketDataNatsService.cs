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
    readonly object gate=new();
    sealed record Memo(string Path,string Digest,Task<MarketDataHostReply> Completion);
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
        ListenAsync(".control",8,stoppingToken),ListenAsync(".stop",2,stoppingToken),ListenAsync(".observe",2,stoppingToken));
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
                MarketDataHostReply reply;
                try
                {
                    var request=message.Data??throw new InvalidDataException("Missing market-data request.");
                    // Emergency work never waits for ordinary deduplication capacity.
                    reply=await (suffix==".stop"?DispatchAsync(request,ct):JoinAsync(request,ct)).ConfigureAwait(false);
                }
                catch(Exception error)when(!ct.IsCancellationRequested)
                {
                    logger.LogError(error,"Native Host RPC failed");
                    reply=new([],TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth.Redact(error.Message));
                }
                await message.ReplyAsync(reply,serializer:NatsMessagePackSerializer<MarketDataHostReply>.Default,cancellationToken:ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        finally{Interlocked.Decrement(ref ready);}
    }
    Task<MarketDataHostReply> JoinAsync(MarketDataHostRequest request,CancellationToken token)
    {
        TaskCompletionSource<MarketDataHostReply> completion;
        var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(request.Payload));
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
            // Reject envelope/signature mismatches before invoking a potentially
            // billable or mutating API operation.
            if(!call.Signature.StartsWith(request.Path[4..]+"(",StringComparison.Ordinal))
                throw new InvalidDataException("Market-data operation signature differs from its envelope.");
            var payload=await new MarketDataApiDispatcher(api).InvokeAsync(call,deadline.Token).ConfigureAwait(false);
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
