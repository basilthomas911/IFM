using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.TradePosition
{
    public class TradePositionService : ITradePositionService
    {
        readonly ITradeEventProducer _tradeEventProducer;
        readonly IOptionPricerCommandApi _optionPricerCommandApi;
        readonly IFundCommandApi _fundCommandApi;
        readonly IFundQueryApi _fundQueryApi;
        readonly IStatusConsoleWriter _statusConsoleWriter;
        readonly ILogger<TradePositionService> _logger;

        /// <summary>
        /// create trade position service
        /// </summary>
        /// <param name="tradeEventProducer"></param>
        /// <param name="optionPricerCommandApi"></param>
        /// <param name="fundCommandApi"></param>
        /// <param name="fundQueryApi"></param>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="logger"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public TradePositionService(
            ITradeEventProducer tradeEventProducer, 
            IOptionPricerCommandApi optionPricerCommandApi,
            IFundCommandApi fundCommandApi,
            IFundQueryApi fundQueryApi,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<TradePositionService> logger)
        {
            _tradeEventProducer = tradeEventProducer ?? throw new ArgumentNullException(nameof(tradeEventProducer));
            _optionPricerCommandApi = optionPricerCommandApi ?? throw new ArgumentNullException(nameof(optionPricerCommandApi));
            _fundCommandApi = fundCommandApi ?? throw new ArgumentNullException(nameof(fundCommandApi));
            _fundQueryApi = fundQueryApi ?? throw new ArgumentNullException(nameof(fundQueryApi));
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
                    TradePositionChangeSourceType.SpreadDistributionStatistics => new OptionTradeDistributionStatisticsUpdatedEvent {
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
        /// execute option trade order placed event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeOrderPlacedEvent e)
        {
            try
            {
                var serviceResult = await _fundQueryApi.GetFundBalanceAsync(e.FundId);
                if (!serviceResult.Success)
                    throw new InvalidOperationException(serviceResult.ErrorMessage);
                var fundBalance = serviceResult.Value.Value;
                var fundTransactions = e.OrderAction switch
                {
                    OrderActionType.Open => new FundTransactionReadModel[] {
                        FundTransactionReadModel
                            .AsOpeningTradeTransaction(
                                fundId: e.FundId,
                                orderId: e.OptionTrade.OrderId,
                                tradeId: e.OptionTrade.TradeId,
                                tradeType: e.OptionTrade.TradeType,
                                valueDate: e.ValueDate,
                                description: string.Empty,
                                amount: fundBalance
                            ),
                        FundTransactionReadModel
                            .AsTradeCommissionTransaction(
                                fundId: e.FundId,
                                orderId: e.OptionTrade.OrderId,
                                tradeId: e.OptionTrade.TradeId,
                                tradeType: e.OptionTrade.TradeType,
                                valueDate: e.ValueDate,
                                tradeStatus: TradeStatus.Open,
                                description: string.Empty,
                                amount: e.TradeCommission
                        )},
                    OrderActionType.Close => new FundTransactionReadModel[] {
                        FundTransactionReadModel
                            .AsRealizedTradePnlTransaction(
                                fundId: e.FundId,
                                orderId: e.OptionTrade.OrderId,
                                tradeId: e.OptionTrade.TradeId,
                                tradeType: e.OptionTrade.TradeType,
                                valueDate: e.ValueDate,
                                description: string.Empty,
                                amount: e.TradePnl
                            ),
                        FundTransactionReadModel
                            .AsTradeCommissionTransaction(
                                fundId: e.FundId,
                                orderId: e.OptionTrade.OrderId,
                                tradeId: e.OptionTrade.TradeId,
                                tradeType: e.OptionTrade.TradeType,
                                valueDate: e.ValueDate,
                                tradeStatus: TradeStatus.Close,
                                description: string.Empty,
                                amount: e.TradeCommission
                        )},
                    _ => throw new InvalidOperationException($"Unknown OrderAction: {e.OrderAction}")
                };
                await _fundCommandApi.CreateFundTransactionsAsync(fundTransactions, e.CommandId);
                await _fundCommandApi.ChangeFundOrderTradeStateAsync(new FundOrderTradeId(e.FundId, e.OptionTrade.OrderId, e.OptionTrade.TradeId), e.OptionTrade.TradeState, e.CommandId);
            }
            catch(Exception ex)
            {
                await _tradeEventProducer.PostEventAsync(e.ToFailedEvent(ex));
                await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.TradePosition, $"{e.GetType().Name} failed due to {ex.GetErrorMessage()}");
                _logger.LogError($"{LogSourceType.TradePosition}: {e.GetType().Name} failed due to {ex.GetErrorMessage()}");
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
                await _tradeEventProducer.PostEventAsync(e.ToFailedEvent(ex));
                await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.TradePosition, ex.GetErrorMessage());
                _logger.LogError($"{LogSourceType.TradePosition}: {e.GetType().Name} failed due to {ex.GetErrorMessage()}");
            }
        }

        /// <summary>
        /// execute option trade end of day processed event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeEndOfDayProcessedEvent e)
        {
            try
            {
                await _fundCommandApi.ProcessEndOfDayFundTransactionAsync(e.CommandId, FundTransactionReadModel
                    .AsUnrealizedTradePnlTransaction(
                        fundId: e.FundId,
                        orderId: e.EodKey.OrderId,
                        tradeId: e.EodKey.TradeId,
                        tradeType: e.EodKey.TradeType,
                        valueDate: e.EodKey.ValueDate,
                        description: e.Reference,
                        amount: e.TradePnl
                    ));
            }
            catch(Exception ex)
            {
                await _tradeEventProducer.PostEventAsync(e.ToFailedEvent(ex));
                await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.TradePosition, ex.GetErrorMessage());
                _logger.LogError($"{LogSourceType.TradePosition}: {e.GetType().Name} failed due to {ex.GetErrorMessage()}");
            }

        }

        /// <summary>
        /// execute option trade distibution statistics changed event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(OptionTradeDistributionStatisticsChangedEvent e)
        {
            try
            {
                await _optionPricerCommandApi.InsertSpreadDistributionsAsync(e.PutSpreadDistribution, e.CallSpreadDistribution);
            }
            catch(Exception ex)
            {
                await _tradeEventProducer.PostEventAsync(e.ToFailedEvent(ex));
                await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.TradePosition, $"{e.GetType().Name} failed due to {ex.GetErrorMessage()}");
                _logger.LogError($"{LogSourceType.TradePosition}: {e.GetType().Name} failed due to {ex.GetErrorMessage()}");
            }
        }
        
    }
}
