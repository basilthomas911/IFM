using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradePlan.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Trade.Commands;

namespace TomasAI.IFM.Application.Command.Client;

public class TradePlanCommandApi(ICommandService commandService) : TomasAI.IFM.Shared.TradePlan.ServiceApi.ITradePlanCommandApi
{
    const string TradePlanController = "TradePlan";
    private readonly ICommandService _commandService = IsArgumentNull.Set(commandService);

    /// <summary>
    /// update trade plan
    /// </summary>
    /// <param name="tradePlan"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> UpdateTradePlanAsync(TradePlanReadModel tradePlan)
        => await new UpdateTradePlanCommand(tradePlan)
            .ExecuteAsync(e => _commandService.PostApiCommandAsync(e, TradePlanController));

    /// <summary>
    /// update iron condor trade plan
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="optionTrades"></param>
    /// <param name="futuresEodData"></param>
    /// <param name="mScore"></param>
    /// <param name="fundBalance"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> UpdateIronCondorTradePlanAsync(
       DateOnly valueDate,
       IOptionTradeCollection optionTrades,
       FuturesEodDataV2ReadModel futuresEodData,
       double mScore,
       decimal fundBalance)
        => await new UpdateIronCondorTradePlanCommand(valueDate, optionTrades, futuresEodData, mScore, fundBalance)
            .ExecuteAsync(e => _commandService.PostApiCommandAsync(e, TradePlanController));

    /// <summary>
    /// update trade plan forward loss limit
    /// </summary>
    /// <param name="forwardLossLimit"></param>
    /// <returns></returns>
    public Task<ServiceResult<Guid>> UpdateTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel forwardLossLimit)
        => new UpdateTradePlanForwardLossLimitCommand(forwardLossLimit)
            .ExecuteAsync(e => _commandService.PostApiCommandAsync(e, TradePlanController));

    /// <summary>
    /// clear trade plan forward loss limit
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public Task<ServiceResult<Guid>> ClearTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId entityId)
        => new ClearTradePlanForwardLossLimitCommand(entityId)
            .ExecuteAsync(e => _commandService.PostApiCommandAsync(e, TradePlanController));
}
