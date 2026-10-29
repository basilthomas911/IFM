using System.Threading.Channels;
using NATS.Client.Core;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.OptionChainCache;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Immediate event-driven prepared-cache delivery. The query mirror
/// supplies catch-up after connection loss; this publisher has no replay timer.</summary>
public sealed class NativeOptionCachePublications(TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache cache,MarketDataHostConnection connection,ILogger<NativeOptionCachePublications> logger,NativeHostState health):BackgroundService
{
    readonly Channel<OptionChainPublication> updates=Channel.CreateBounded<OptionChainPublication>(new BoundedChannelOptions(256){SingleReader=true,FullMode=BoundedChannelFullMode.DropOldest});
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        cache.Published+=Published;
        try
        {
            var client=await connection.GetClientAsync(stoppingToken).ConfigureAwait(false);
            while(await updates.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                var latest=new Dictionary<string,OptionChainPublication>(StringComparer.Ordinal);
                while(updates.Reader.TryRead(out var update))latest[update.LookupScope]=update;
                foreach(var update in latest.Values)
                    try{foreach(var frame in OptionChainPublicationFrame.Encode(update))await client.Connection.PublishAsync(connection.Scope+".option-cache",frame,serializer:NatsMessagePackSerializer<OptionChainPublicationFrame>.Default,cancellationToken:stoppingToken).ConfigureAwait(false);}
                    catch(Exception error)when(!stoppingToken.IsCancellationRequested){health.ReportProcessingFailure("PreparedCachePublication",error);logger.LogError(error,"Native prepared-cache publication failed; Scope={Scope}; Catch-up remains available in the API snapshot",update.LookupScope);}
            }
        }
        catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){}
        finally{cache.Published-=Published;}
    }
    void Published(OptionChainPublication update)=>updates.Writer.TryWrite(update);
}
