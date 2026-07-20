using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.MarketDataFeed.EventHandlers;

/// <summary>
/// futures eod data event handlers constructor
/// </summary>
/// <param name="queryApi"></param>
/// <param name="eventProducer"></param>
/// <param name="blackBoardService"></param>
/// <param name="statusConsoleWriter"></param>
/// <param name="logger"></param>
public class FuturesEodDataEventHandlers(
    IMarketDataFeedQueryApi queryApi,
    IMarketDataFeedEventProducer eventProducer,
    IBlackboardService blackBoardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesEodDataEventHandlers> logger) : BaseEventServiceHandler(statusConsoleWriter),
    IAsyncEventHandler<FuturesEodDataInsertedEvent, MarketDataFeedService>,
    IAsyncEventHandler<VixFuturesEodDataInsertedCompleteEvent, MarketDataFeedService>
{
    IMarketDataFeedQueryApi QueryApi { get; } = queryApi ?? throw new ArgumentNullException(nameof(queryApi));
    IMarketDataFeedEventProducer EventProducer { get; } = eventProducer ?? throw new ArgumentNullException(nameof(eventProducer));
    IBlackboardService BlackBoard { get; } = blackBoardService ?? throw new ArgumentNullException(nameof(blackBoardService));
    ILogger<FuturesEodDataEventHandlers> Logger { get; } = logger ?? throw new ArgumentNullException(nameof(logger));


    /// <summary>
    /// insert futures eod data
    /// </summary>
    /// <param name="e">futures eod data inserted event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(FuturesEodDataInsertedEvent e)
    {
        try
        {
            BlackBoard.FuturesEodData.Set(e.FuturesEodData.ContractId, e.FuturesEodData.ValueDate, e.FuturesEodData);
                await EventProducer.PostEventAsync(new FuturesEodDataUpdatedEvent
                {
                    Subject = new ActorSubject(ActorType.Event, FuturesEodDataUpdatedEvent.Actor, FuturesEodDataUpdatedEvent.Verb, e.EntityId.Format()),
                    EntityId = e.EntityId,
                    CommandId = e.CommandId,
                    AggregateId = e.AggregateId,
                    EventSource = e.EventSource,
                    ReceivedOn = e.ReceivedOn,
                    FuturesEodData = e.FuturesEodData,
                    UpdatedOn = DateTime.Now,
                    UpdatedBy = e.UserName
                });
            Logger.LogInformation($"{LogSourceType.MarketDataFeedService}: futures eod data {e.FuturesEodData.ContractId} {Convert.ToDecimal(e.FuturesEodData.ClosePrice)}");
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, FuturesEodDataInsertedEvent.ErrorCode, ex.GetErrorMessage());
            Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataFeedService}: futures eod data {e.FuturesEodData.ContractId} insert failed");
        }
    }

    /// <summary>
    /// cache vix futures eod data 
    /// </summary>
    /// <param name="e">vix futures eod data inserted complete event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(VixFuturesEodDataInsertedCompleteEvent e)
    {
        try
        {
            var serviceResult = await QueryApi.GetVixFuturesEodDataAsync(e.VixFuturesTickData.ContractId, e.VixFuturesTickData.ValueDate);
            if (serviceResult.Success)
            {
                var vixFuturesEodData = serviceResult.Value;
                if (vixFuturesEodData != null)
                {
                    BlackBoard.VixFuturesEodData.Set(e.VixFuturesTickData.ContractId, e.VixFuturesTickData.ValueDate, vixFuturesEodData);
                    await WriteConsoleAsync(LogSourceType.MarketDataFeedService, $"{e.VixFuturesTickData.ContractId}:={e.VixFuturesTickData.Price} cached");
                    Logger.LogInformation($"{LogSourceType.MarketDataFeedService}: {e.VixFuturesTickData.ContractId}:={e.VixFuturesTickData.Price} cached");
                }
            }
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.MarketDataFeedService, 6009, ex.GetErrorMessage());
            Logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.MarketDataFeedService}: vix futures eod data {e.VixFuturesTickData.ContractId} caching failed");
        }
    }

}
