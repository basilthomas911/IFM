using System.Threading.Channels;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Optional historical VX fallback, separate from native/live state.
/// Session transitions wake the owner; there is no market-data polling loop.
/// A missing estimate retries once per minute under a new optional query budget,
/// never extending or participating in the live recovery deadline.</summary>
public sealed class NativeBootstrapPrices(IServiceProvider services,NativeMarketDataEpochFactory epochs,
    NativeHostState state,TimeProvider clock,ILogger<NativeBootstrapPrices> logger):BackgroundService
{
    readonly object gate=new();
    readonly Channel<DateOnly> changed=Channel.CreateBounded<DateOnly>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true});
    DateOnly? date;CancellationTokenSource? session;
    MarketDataBootstrapPrice[] values=[];
    /// <summary>Starts an API-supplied date and fences the prior estimate/query.</summary>
    public void StartSession(DateOnly valueDate)
    {
        if(valueDate==default)throw new ArgumentOutOfRangeException(nameof(valueDate));
        lock(gate){if(date==valueDate&&session is{IsCancellationRequested:false})return;session?.Cancel();session?.Dispose();session=new();date=valueDate;values=[];changed.Writer.TryWrite(valueDate);}
    }
    /// <summary>Revokes fallback authority on market close/session shutdown.</summary>
    public void StopSession(DateOnly valueDate)
    {lock(gate){if(date!=valueDate)return;session?.Cancel();date=null;values=[];}}
    /// <summary>Copies only the current date's explicitly historical estimates.</summary>
    public MarketDataBootstrapPrice[] Capture(DateOnly? valueDate)
    {lock(gate)return date==valueDate&&valueDate is not null?values.ToArray():[];}
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach(var requested in changed.Reader.ReadAllAsync(stoppingToken))
            {
                CancellationToken owned;lock(gate){if(date!=requested||session is null)continue;owned=session.Token;}
                using var linked=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken,owned);
                try
                {
                    while(!linked.IsCancellationRequested)
                    {
                        if(!epochs.TryGetCurrent(out var epoch)||epoch!.ValueDate!=requested)break;
                        var vx=state.CapturePrimaryManifests().SelectMany(x=>x.Contracts).FirstOrDefault(x=>x.RootSymbol=="VX"&&x.OnTheRun);
                        if(vx is null||epoch.TryGetLastTickPrice(vx.DomainContractId,out var live)&&live.Trade is not null)break;
                        try
                        {
                            using var query=CancellationTokenSource.CreateLinkedTokenSource(linked.Token);query.CancelAfter(TimeSpan.FromSeconds(30));
                            // Resolve lazily: an unstarted Host needs no provider key,
                            // database/Redis connection or historical HTTP request.
                            var loader=new NativeVxBootstrapLoader(services.GetRequiredService<IDbContextFactory>(),services.GetRequiredService<IMarketDataHistoricalProvider>(),clock,services.GetRequiredService<ILogger<NativeVxBootstrapLoader>>());
                            var result=await loader.LoadAsync(vx,requested,query.Token).ConfigureAwait(false);
                            if(result is{} seed&&seed.Price.Trade is{} trade&&trade.LastPrice>0&&trade.EventTimestamp<=clock.GetUtcNow()
                                &&trade.NormalizedTradeAction is not(NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear)
                                &&(trade.NormalizedTradeConditionFlags&NormalizedTradeConditionFlags.UndefinedPrice)==0)
                            {
                                lock(gate){if(date==requested&&!owned.IsCancellationRequested)values=[new(seed.Price,seed.Source)];}
                                logger.LogInformation("VX historical bootstrap ready; Contract={Contract}; ValueDate={ValueDate}; Source={Source}; EventUtc={EventUtc}. Live readiness unchanged.",vx.DomainContractId,requested,seed.Source,trade.EventTimestamp);
                                break;
                            }
                        }
                        catch(OperationCanceledException)when(linked.IsCancellationRequested){break;}
                        catch(Exception error){logger.LogWarning(error,"Optional VX bootstrap unavailable; ValueDate={ValueDate}. Live recovery continues independently.",requested);}
                        await Task.Delay(TimeSpan.FromMinutes(1),clock,linked.Token).ConfigureAwait(false);
                    }
                }
                catch(OperationCanceledException)when(linked.IsCancellationRequested){}
            }
        }
        catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){}
    }
    public override void Dispose(){lock(gate){session?.Cancel();session?.Dispose();session=null;values=[];date=null;}base.Dispose();}
}
