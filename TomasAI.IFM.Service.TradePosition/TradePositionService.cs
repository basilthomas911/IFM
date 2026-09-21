using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.TradePosition;

public class TradePositionService : ITradePositionService
{
    readonly ITradeEventProducer _tradeEventProducer;
    readonly IOptionPricerCommandApi _optionPricerCommandApi;
    readonly IStatusConsoleWriter _statusConsoleWriter;
    readonly ILogger<TradePositionService> _logger;

    /// <summary>
    /// create trade position service
    /// </summary>
    /// <param name="tradeEventProducer"></param>
    /// <param name="optionPricerCommandApi"></param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public TradePositionService(
        ITradeEventProducer tradeEventProducer, 
        IOptionPricerCommandApi optionPricerCommandApi,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<TradePositionService> logger)
    {
        _tradeEventProducer = tradeEventProducer ?? throw new ArgumentNullException(nameof(tradeEventProducer));
        _optionPricerCommandApi = optionPricerCommandApi ?? throw new ArgumentNullException(nameof(optionPricerCommandApi));
        _statusConsoleWriter = statusConsoleWriter ?? throw new ArgumentNullException(nameof(statusConsoleWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("TradePositionService started");
    }

    /// <summary>
    /// execute trade position changed event
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(TradePositionChangedEvent e)
    {
        try
        {
            IEvent updatedEvent = e.TradePositionChangeSource switch {
                TradePositionChangeSourceType.PutCreditSpreadLeg => new TradePositionUpdatedEvent {
                    CommandId = e.CommandId,
                    TradePositionChangeSource = TradePositionChangeSourceType.PutCreditSpreadLeg,
                    PutTradePosition = e.PutTradePosition,
                    CallTradePosition = e.CallTradePosition,
                    OptionLegId = e.OptionLegId,
                    UpdatedOn = e.UpdatedOn,
                    UpdatedBy = e.UpdatedBy },
                TradePositionChangeSourceType.CallCreditSpreadLeg => new TradePositionUpdatedEvent {
                    CommandId = e.CommandId,
                    TradePositionChangeSource = TradePositionChangeSourceType.CallCreditSpreadLeg,
                    PutTradePosition = e.PutTradePosition,
                    CallTradePosition = e.CallTradePosition,
                    OptionLegId = e.OptionLegId,
                    UpdatedOn = e.UpdatedOn,
                    UpdatedBy = e.UpdatedBy },
                TradePositionChangeSourceType.SpreadDistributionStatistics => new OptionTradeSpreadDistributionStatisticsChangedEvent {
                    CommandId = e.CommandId,
                    OrderId = e.TradePositionId.OrderId,
                    TradeId = e.TradePositionId.TradeId,
                    ForwardLossRatio = e.PutTradePosition.LossProbability,
                    ValueDate = e.TradePositionId.ValueDate },
                _ => default
            };
            if (updatedEvent is not null)
                await _tradeEventProducer.PostEventAsync(updatedEvent);
        }
        catch (Exception ex)
        {
            await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.TradePosition, $"{e.GetType().Name} failed due to {ex}");
            _logger.LogError($"{LogSourceType.TradePosition}: {e.GetType().Name} failed due to {ex}");
        }
    }

    /// <summary>
    /// execute option trade leg data changed event
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(OptionTradeLegDataChangedEvent e)
    {
        try
        {
            await _tradeEventProducer.PostEventAsync(new OptionTradeLegDataUpdatedEvent
            {
                CommandId = e.CommandId,
                Key = e.Key,
                OptionLegData = e.OptionLegData,
                OrderId = e.OrderId,
                AssetPrice = e.AssetPrice,
                CreatedOn = e.CreatedOn,
                CreatedBy = e.CreatedBy,
                UpdatedOn = e.UpdatedOn,
                UpdatedBy = e.UpdatedBy
            });
        }
        catch(Exception ex)
        {
            await _tradeEventProducer.PostEventAsync(e.ToFailEvent<OptionTradeLegDataChangedFailEvent, OptionTradeEntityId>(ex));
            await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.TradePosition, ex.GetErrorMessage());
            _logger.LogError($"{LogSourceType.TradePosition}: {e.GetType().Name} failed due to {ex.GetErrorMessage()}");
        }
    }

    /// <summary>
    /// execute option trade distibution statistics changed event
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(OptionTradeSpreadDistributionStatisticsUpdatedEvent e)
    {
        try
        {
            await _optionPricerCommandApi.InsertSpreadDistributionsAsync(
                e.PutSpreadDistribution.ToSpreadDistributionReadModel(),
                e.CallSpreadDistribution.ToSpreadDistributionReadModel());
        }
        catch(Exception ex)
        {
            await _tradeEventProducer.PostEventAsync(e.ToFailEvent<OptionTradeSpreadDistributionStatisticsUpdatedFailEvent, OptionTradeEntityId>(ex));
            await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.TradePosition, $"{e.GetType().Name} failed due to {ex.GetErrorMessage()}");
            _logger.LogError($"{LogSourceType.TradePosition}: {e.GetType().Name} failed due to {ex.GetErrorMessage()}");
        }
    }
    
}
