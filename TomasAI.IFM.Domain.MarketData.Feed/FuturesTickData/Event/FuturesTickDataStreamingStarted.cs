using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

public static class FuturesTickDataStreamingStarted
{
    static FuturesTickDataStreamingStarted()
    {
        ServiceId = $"{LogSourceType.FuturesTickDataEvent}";
    }

    static string ServiceId { get; }

public static async ValueTask<bool> ExecuteAsync(
    this FuturesTickDataStreamingStartedEvent e,
    IEventActorContext context,
    IActorMarketDataFeedEventApi eventApi,
    FuturesTickDataEventParameters p)
    {
        var source = $"FuturesTickDataStreamingStartedEvent for EntityId: {e.EntityId}";
        try
        {
            await p.MarketDataApi.StartAsync(
                e.ValueDate,
                (_, errorCode, errorMsg) => p.StatusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.FuturesTickDataEvent, errorCode, errorMsg));
            var owner = CreateOwner(e.EntityId, e.Contract.ContractId);
            _ = await p.MarketDataApi.StartStreamingFuturesTickDataAsync(
                e.Contract.ContractId,
                owner).ConfigureAwait(false);
            p.Streams.Track(owner, e.Contract.ContractId, e.Contract);
            if (e.Contract.Id.IsVixContract)
            {
                p.BlackboardService.MarketDataFeed.VixFuturesContractId.Set(
                    e.ValueDate,
                    e.Contract.ContractId);
            }
            await eventApi.FuturesTickDataStreamingStartedCompleteAsync(e);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, $"Futures {e.Contract.ContractId} streaming started");
            p.Logger.LogInformationEvent(ServiceId, "{Source}: futures {e.Contract.ContractId} streaming started", source, e.Contract.ContractId);
            return true;
        }
        catch (Exception ex)
        {
            await eventApi.FuturesTickDataStreamingStartedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, 6003, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures {e.Contract.ContractId} streaming start failed", source, e.Contract.ContractId);
        }
        return false;
    }

    internal static TickerStreamOwner CreateOwner(
        FuturesTickDataStreamingId entityId,
        string contractId) => new(
        nameof(FuturesTickDataEventActor),
        entityId.Format(),
        contractId);
}
