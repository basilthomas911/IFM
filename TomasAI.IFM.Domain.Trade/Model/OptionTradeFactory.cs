using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
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
        optionTrade.AddOptionLegs(otvm.OptionLegs!.Select(ol => new OptionLeg(
                orderId: ol.OrderId,
                tradeId: ol.TradeId,
                contractId: ol.ContractId,
                quantity: ol.Quantity,
                strikePrice: ol.StrikePrice,
                optionLegType: ol.OptionLegType,
                optionLegAction: ol.OptionLegAction,
                createdOn: createdOn,
                createdBy: optionTrade.CreatedBy,
                updatedOn: createdOn,
                updatedBy: optionTrade.CreatedBy
            )).Cast<IOptionLeg>().ToList());

        // add trade position including option leg data...
        optionTrade.AddTradePositions((otvm.TradePositions ?? []).Select(o =>
            new TradePosition(o, createdOn, createdBy)
                .AddOptionLegData(o.OptionLegData.Select(x => new OptionLegData(o.EntityId, x.SetOptionLeg(optionTrade.OptionLegs.Where(ol => ol.ContractId == x.OptionLegId).Single().ToDataModel()),
                    createdOn, createdBy, createdOn, createdBy)).Cast<IOptionLegData>().ToList()))
                .ToList());

        // set trade limit...
        if (otvm.TradeLimit is null)
            throw new InvalidOperationException("OptionTradeFactory.Create: TradeLimit is required.");
        optionTrade.SetTradeLimit(new TradeLimit(otvm.TradeLimit, optionTrade.CreatedOn, optionTrade.CreatedBy, optionTrade.CreatedOn, optionTrade.CreatedBy));

        // add trade type limits...
        optionTrade.AddTradeTypeLimits((otvm.TradeTypeLimits ?? [])
            .Select(o => new TradeTypeLimit(o.TradeId, o.TradeType, o.MaxLossLimit, o.MinProfitLimit, o.MaxProfitLimit))
            .ToList<ITradeTypeLimit>());

        // add trade fills if passed...
        if (otvm.TradeFills != null)
            optionTrade.AddTradeFills(otvm.TradeFills.Select(o => new TradeFill(o)).ToList<ITradeFill>(), optionTrade.CreatedOn, optionTrade.CreatedBy);
        return optionTrade;
    }
}
