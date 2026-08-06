using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Domain.Trade.Model.Strategy;

namespace TomasAI.IFM.Domain.Trade.Model;

public static class OptionTradeFactory
{
    /// <summary>
    /// create option trade from trade ticket
    /// </summary>
    /// <param name="tradeTicket"></param>
    /// <returns></returns>
    public static OptionTrade Create(TradeOrderReadModel tradeOrder, TradeState tradeState)
    {
        var optionTrade = tradeOrder.TradeType switch
        {
            TradeType.ShortIronCondor => IronCondorTrade.Create(tradeOrder, tradeState),
            TradeType.LongIronCondor => IronCondorTrade.Create(tradeOrder, tradeState),
            _ => throw new NotImplementedException($"OptionTradeFactory.Create: unable to create {tradeOrder.TradeType} from trade order")
        };

        // add option legs...
        optionTrade.AddOptionLegs(tradeOrder.OptionLegs.Select(ol => new OptionLeg(
                orderId: ol.OrderId,
                tradeId: ol.TradeId,
                contractId: ol.ContractId,
                quantity: ol.Quantity,
                strikePrice: ol.StrikePrice,
                optionLegType: ol.OptionLegType,
                optionLegAction: ol.OptionLegAction,
                createdOn: optionTrade.CreatedOn,
                createdBy: optionTrade.CreatedBy,
                updatedOn: optionTrade.CreatedOn,
                updatedBy: optionTrade.CreatedBy
            )).Cast<IOptionLeg>().ToList());
       
        // set trade limit...
        optionTrade.SetTradeLimit(new TradeLimit(tradeOrder.TradeLimit, optionTrade.CreatedOn, optionTrade.CreatedBy, optionTrade.CreatedOn, optionTrade.CreatedBy));

        // add trade type limits...
        optionTrade.AddTradeTypeLimits([.. tradeOrder.TradeTypeLimits.Select(o => new TradeTypeLimit(o.TradeId, o.TradeType, o.MaxLossLimit, o.MinProfitLimit, o.MaxProfitLimit))]);

        // add trade fills if passed...
        if (tradeOrder.TradeFills != null)
            optionTrade.AddTradeFills([.. tradeOrder.TradeFills.Select(o => new TradeFill(o))], optionTrade.CreatedOn, optionTrade.CreatedBy);
        return optionTrade;
    }

    /// <summary>
    /// create option trade from option trade view model
    /// </summary>
    /// <param name="otvm"></param>
    /// <returns></returns>
    public static OptionTrade Create(OptionTradeReadModel otvm)
    {
        var optionTrade = default(OptionTrade);
        switch (otvm.TradeType)
        {
            case TradeType.ShortIronCondor:
                optionTrade = IronCondorTrade.Create(otvm);
                break;
        }

        if (optionTrade is null)
            throw new InvalidOperationException($"OptionTradeFRactory.Create: unable to create {otvm.TradeType} from option trade");

        var createdOn = otvm.CreatedOn;
        var createdBy = otvm.CreatedBy;

        // add option legs...
        var optionLegModels = otvm.OptionLegs ?? [];
        var optionLegs = new List<IOptionLeg>(optionLegModels.Length);
        var optionLegById = new Dictionary<string, IOptionLeg>(optionLegModels.Length, StringComparer.Ordinal);
        foreach (var optionLegModel in optionLegModels)
        {
            IOptionLeg optionLeg = new OptionLeg(
                orderId: optionLegModel.OrderId,
                tradeId: optionLegModel.TradeId,
                contractId: optionLegModel.ContractId,
                quantity: optionLegModel.Quantity,
                strikePrice: optionLegModel.StrikePrice,
                optionLegType: optionLegModel.OptionLegType,
                optionLegAction: optionLegModel.OptionLegAction,
                createdOn: createdOn,
                createdBy: optionTrade.CreatedBy,
                updatedOn: createdOn,
                updatedBy: optionTrade.CreatedBy);
            optionLegs.Add(optionLeg);
            optionLegById.Add(optionLeg.ContractId, optionLeg);
        }
        optionTrade.AddOptionLegs(optionLegs);

        // add trade position including option leg data...
        var positionModels = otvm.TradePositions ?? [];
        var tradePositions = new List<ITradePosition>(positionModels.Length);
        foreach (var positionModel in positionModels)
        {
            var legData = new List<IOptionLegData>(positionModel.OptionLegData.Length);
            foreach (var legDataModel in positionModel.OptionLegData)
            {
                legData.Add(new OptionLegData(
                    positionModel.EntityId,
                    legDataModel.SetOptionLeg(optionLegById[legDataModel.OptionLegId].ToDataModel()),
                    createdOn,
                    createdBy,
                    createdOn,
                    createdBy));
            }
            tradePositions.Add(new TradePosition(positionModel, createdOn, createdBy).AddOptionLegData(legData));
        }
        optionTrade.AddTradePositions(tradePositions);

        // set trade limit...
        if (otvm.TradeLimit is null)
            throw new InvalidOperationException("OptionTradeFactory.Create: TradeLimit is required.");
        optionTrade.SetTradeLimit(new TradeLimit(otvm.TradeLimit, optionTrade.CreatedOn, optionTrade.CreatedBy, optionTrade.CreatedOn, optionTrade.CreatedBy));

        // add trade type limits...
        var typeLimitModels = otvm.TradeTypeLimits ?? [];
        var tradeTypeLimits = new List<ITradeTypeLimit>(typeLimitModels.Length);
        foreach (var limit in typeLimitModels)
            tradeTypeLimits.Add(new TradeTypeLimit(limit.TradeId, limit.TradeType, limit.MaxLossLimit, limit.MinProfitLimit, limit.MaxProfitLimit));
        optionTrade.AddTradeTypeLimits(tradeTypeLimits);

        // add trade fills if passed...
        if (otvm.TradeFills != null)
        {
            var tradeFills = new List<ITradeFill>(otvm.TradeFills.Length);
            foreach (var tradeFill in otvm.TradeFills)
                tradeFills.Add(new TradeFill(tradeFill));
            optionTrade.AddTradeFills(tradeFills, optionTrade.CreatedOn, optionTrade.CreatedBy);
        }
        return optionTrade;
    }
}
