using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public static class FuturesOptionTickDataStreamingStarted
{
    static FuturesOptionTickDataStreamingStarted()
    {
        ServiceId = $"{LogSourceType.FuturesOptionTickDataEvent}";
    }

    static string ServiceId { get; }

public static async ValueTask<bool> ExecuteAsync(
    this FuturesOptionTickDataStreamingStartedEvent e,
    IEventActorContext context,
    IActorMarketDataFeedEventApi eventApi,
    FuturesOptionTickDataEventParameters p)
    {
        var source = $"FuturesOptionTickDataStreamingStartedEvent for EntityId: {e.EntityId}";
        try
        {
            await p.MarketDataApi.StartAsync(
                e.ValueDate,
                (_, errorCode, errorMsg) => p.StatusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.FuturesOptionTickDataEvent, errorCode, errorMsg));
            _ = await p.MarketDataApi.GetFuturesOptionContractAsync(
                e.Contract.ContractId)
                ?? throw new InvalidOperationException(
                    $"Futures option contract '{e.Contract.ContractId}' is not configured in the active market-data epoch.");
            var owner = CreateOwner(e.EntityId, e.Contract.ContractId);
            _ = await p.MarketDataApi.StartStreamingFuturesOptionTickDataAsync(
                e.Contract.ContractId,
                owner).ConfigureAwait(false);
            p.Streams.Track(owner, e.Contract.ContractId, e.Contract);
            await eventApi.SendFuturesOptionTickDataStreamingStartedCompleteAsync(e);

            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, $"futures option {e.Contract.ContractId} streaming started");
            p.Logger.LogInformationEvent("{Source}: futures option {ContractId} streaming started", source, e.Contract.ContractId);
            return true;
        }
        catch (Exception ex)
        {
            await eventApi.SendFuturesOptionTickDataStreamingStartedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, FuturesOptionTickDataStreamingStartedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures option {ContractId} streaming start failed", source, e.Contract.ContractId);
        }
        return false;
    }

    internal static TickerStreamOwner CreateOwner(
        FuturesOptionTickEntityId entityId,
        string contractId) => new(
        nameof(FuturesOptionTickDataEventActor),
        entityId.Format(),
        contractId);
}
