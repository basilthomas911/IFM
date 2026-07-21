using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Option.Command.State;

/// <summary>
/// Represents the event-sourced state of Option Trade commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Option Trade operations by applying domain events.
/// It mirrors the logic of <see cref="OptionTradeBoundedContextState"/>.</remarks>
public class OptionTradeCommandState
    : BaseEventSourceActorState<OptionTradeCommandState>, IEventSourceActorState<OptionTradeCommandState>
{
    IOptionTrade? _optionTrade;
    TradePositionState _tradePositionState;

    /// <summary>
    /// Gets or sets the unique identifier for the actor thread associated with this state.
    /// </summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// Applies the specified domain event to update the state of the current object.
    /// </summary>
    /// <param name="domainEvent">The domain event to apply. Must be of a supported type.</param>
    /// <returns><see langword="true"/> if the domain event was successfully applied; otherwise, <see langword="false"/>.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                OptionTradeOrderPlacedEvent e => On(e),
                OptionTradeToOpenEvent e => On(e),
                OptionTradeToCloseEvent e => On(e),
                OptionTradeSnapshotEvent e => On(e),
                TradePositionAddedEvent e => On(e),
                OptionTradeLegDataChangedEvent e => On(e),
                OptionTradeSpreadDistributionStatisticsUpdatedEvent e => On(e),
                OptionTradeEndOfDayProcessedEvent e => On(e),
                OptionTradeDeletedEvent e => On(e),
                OptionTradeDailyProfitTargetUpdatedEvent e => On(e),
                OptionTradePositionOpenedEvent e => On(e),
                OptionTradePositionClosedEvent e => On(e),
                _ => false
            }; ;
        }
        catch { }
        return false;
    }

    /// <summary>
    /// option trade order placed
    /// </summary>
    /// <param name="e">spread trade order placed event</param>
    bool On(OptionTradeOrderPlacedEvent e)
    {
        SetOptionTrade(e.OptionTrade, e.CreatedOn, e.CreatedBy);
        EventInitHelper.SetProperty(e, nameof(OptionTradeOrderPlacedEvent.OptionTrade), _optionTrade!.ToReadModel());
        return true;
    }

    /// <summary>
    /// created opening option trade
    /// </summary>
    /// <param name="e"></param>
    bool On(OptionTradeToOpenEvent e)
    {
        _optionTrade = OptionTradeFactory.Create(e.TradeOrder, TradeState.TradeToOpen);
        EventInitHelper.SetProperty(e, nameof(OptionTradeToOpenEvent.OptionTrade), _optionTrade.ToReadModel());
        return true;
    }

    /// <summary>
    /// created closing option trade
    /// </summary>
    /// <param name="e"></param>
    bool On(OptionTradeToCloseEvent e)
    {
        _optionTrade = OptionTradeFactory.Create(e.TradeOrder, TradeState.TradeToClose);
        EventInitHelper.SetProperty(e, nameof(OptionTradeToCloseEvent.OptionTrade), _optionTrade.ToReadModel());
        return true;
    }

    /// <summary>
    /// snapshot current option trade
    /// </summary>
    /// <param name="e"></param>
    bool On(OptionTradeSnapshotEvent e)
    {
        SetOptionTrade(e.OptionTrade, e.CreatedOn, e.CreatedBy);
        EventInitHelper.SetProperty(e, nameof(OptionTradeSnapshotEvent.OptionTrade), _optionTrade!.ToReadModel());
        return true;
    }

    /// <summary>
    /// create option trade
    /// </summary>
    /// <param name="optionTrade"></param>
    /// <param name="createdOn"></param>
    /// <param name="createdBy"></param>
    void SetOptionTrade(OptionTradeReadModel optionTrade, DateTime createdOn, string createdBy)
    {
        // create new spread trade...
        _optionTrade = OptionTrade.Create(
            orderId: optionTrade.OrderId,
            tradeId: optionTrade.TradeId,
            tradeStrategy: optionTrade.TradeStrategy,
            tradeDate: optionTrade.TradeDate,
            maturityDate: optionTrade.MaturityDate,
            tradeType: optionTrade.TradeType,
            tradeState: optionTrade.TradeState,
            tradeAction: optionTrade.TradeAction,
            underlyingContractId: optionTrade.UnderlyingContractId,
            underlyingAssetType: optionTrade.UnderlyingAssetType,
            isPrimaryTrade: optionTrade.IsPrimaryTrade,
            isHedgeTrade: optionTrade.IsHedgeTrade,
            createdOn: createdOn,
            createdBy: createdBy,
            updatedOn: createdOn,
            updatedBy: createdBy
        );

        // add option legs...
        if (optionTrade.OptionLegs is not null && optionTrade.OptionLegs.Length > 0)
            _optionTrade.AddOptionLegs([.. optionTrade.OptionLegs.Select(ol => new OptionLeg(
                orderId: ol.OrderId,
                tradeId: ol.TradeId,
                contractId: ol.ContractId,
                quantity: ol.Quantity,
                strikePrice: ol.StrikePrice,
                optionLegType: ol.OptionLegType,
                optionLegAction: ol.OptionLegAction,
                createdOn: createdOn,
                createdBy: createdBy,
                updatedOn: createdOn,
                updatedBy: createdBy
            )).Cast<IOptionLeg>()]);

        // add trade position including option leg data...
        if (optionTrade.TradePositions is not null && optionTrade.TradePositions.Length > 0)
            _optionTrade.AddTradePositions([.. optionTrade.TradePositions.Select(o =>
                new TradePosition(o, createdOn, createdBy)
                    .AddOptionLegData([.. o.OptionLegData.Select(x => new OptionLegData(o.EntityId, x.SetOptionLeg(_optionTrade.OptionLegs.Where(ol => ol.ContractId == x.OptionLegId).Single().ToDataModel()), 
                        createdOn, createdBy, createdOn, createdBy)).Cast<IOptionLegData>()]))]);

        // set trade limit...
        if (optionTrade.TradeLimit is not  null)
            _optionTrade.SetTradeLimit(new TradeLimit(optionTrade.TradeLimit, createdOn, createdBy, createdOn, createdBy));

        // add trade type limits...
        if (optionTrade.TradeTypeLimits is not null && optionTrade.TradeTypeLimits.Length > 0)
                _optionTrade.AddTradeTypeLimits([.. optionTrade.TradeTypeLimits.Select(o => new TradeTypeLimit(o.TradeId, o.TradeType, o.MaxLossLimit, o.MinProfitLimit, o.MaxProfitLimit))]);

        // add trade fills if passed...
        if (optionTrade.TradeFills != null)
            _optionTrade.AddTradeFills([.. optionTrade.TradeFills.Select(o => new TradeFill(o))], createdOn, createdBy);

    }

    /// <summary>
    /// option trade data added
    /// </summary>
    /// <param name="e"></param>
    bool On(TradePositionAddedEvent e)
    { 
        _optionTrade?.AddTradePosition(
            new TradePosition(e.TradePosition, e.CreatedOn, e.CreatedBy)
                .AddOptionLegData([.. e.TradePosition
                    .OptionLegData.Select(optionLegData => new OptionLegData(e.TradePosition.EntityId, optionLegData, e.CreatedOn, e.CreatedBy, e.CreatedOn, e.CreatedBy))]));
        return true;
    }

    /// <summary>
    /// update option trade data with option leg price changes
    /// </summary>
    /// <param name="e"></param>
    bool On(OptionTradeLegDataChangedEvent e)
    {
        if (_optionTrade?.TradePositions.Count > 0)
        {
            var tradePosition = _optionTrade.TradePositions.IntraDay(e.Key.TradeType, e.Key.ValueDate);
            if (tradePosition is not null)
            { 
                tradePosition.ReplaceOptionLegData(new OptionLegData(e.Key, e.OptionLegData, e.UpdatedOn, e.UpdatedBy, e.UpdatedOn, e.UpdatedBy))
                    .SetAssetPrice(e.AssetPrice)
                    .SetUpdated(e.UpdatedOn, e.UpdatedBy);
                _optionTrade.TradePositions.SetTradePnl(e.Key.TradeType);
            }
        }
        return true;
    }

    /// <summary>
    /// update option trade distribution statistics changes
    /// </summary>
    /// <param name="e"></param>
    bool On(OptionTradeSpreadDistributionStatisticsUpdatedEvent e)
    {
        var pcsTradeData = _optionTrade?.TradePositions.IntraDay(PutSpreadTradeType, e.ValueDate);
        if (e.PutSpreadDistribution is not null)
        {
            pcsTradeData?.SetForwardPrice(Convert.ToDecimal(e.PutSpreadDistribution.ForwardPrice));
            pcsTradeData?.SetLossProbability(e.PutSpreadDistribution.LossProbability);
        }
        var ccsTradeData = _optionTrade?.TradePositions.IntraDay(CallSpreadTradeType, e.ValueDate);
        if (e.CallSpreadDistribution is not null)
        {
            ccsTradeData?.SetForwardPrice(Convert.ToDecimal(e.CallSpreadDistribution.ForwardPrice));
            ccsTradeData?.SetLossProbability(e.CallSpreadDistribution.LossProbability);
        }
        var forwardLossRatio = GetForwardLossRatio();
        var lossProbability = GetLossProbability();

        if (!double.IsNaN(forwardLossRatio) && !double.IsNaN(lossProbability))
        {
            if (e.PutSpreadDistribution is not null)
            {
                EventInitHelper.SetProperty(e, nameof(OptionTradeSpreadDistributionStatisticsUpdatedEvent.PutSpreadDistribution), e.PutSpreadDistribution with { ForwardLossRatio = forwardLossRatio });
                if (_optionTrade?.TradePositions is not null && _optionTrade.TradePositions.Count > 0)
                    _optionTrade?.TradePositions
                        .IntraDay(PutSpreadTradeType, e.ValueDate)!
                        .SetForwardLossRatio(forwardLossRatio)
                        .SetLossProbability(lossProbability)
                        .SetUpdated(e.UpdatedOn, e.UpdatedBy);
                }
        }
        return true;

        double GetForwardLossRatio()
        {
            if (pcsTradeData == null || ccsTradeData == null) return double.NaN;
            var netForwardPrice = this.TradeType == TradeType.ShortIronCondor 
                ? Math.Abs( (pcsTradeData.ForwardPrice - ccsTradeData.ForwardPrice) )
                : Math.Abs(pcsTradeData.ForwardPrice );
            netForwardPrice = netForwardPrice < 0.0m ? 0.0m : netForwardPrice;
            var limitPrice = this.TradeType == TradeType.ShortIronCondor ? this.TradeLimit.MaxLossLimit : this.TradeLimit.MinProfitLimit;
            return Convert.ToDouble(Math.Min(netForwardPrice / limitPrice, 1.0m));
        }

        double GetLossProbability()
            => pcsTradeData is null || ccsTradeData is null
                ? double.NaN 
                : pcsTradeData.LossProbability != 0.0 ? pcsTradeData.LossProbability : ccsTradeData.LossProbability;

    }

    /// <summary>
    /// process end of day event
    /// </summary>
    /// <param name="e"></param>
    bool On(OptionTradeEndOfDayProcessedEvent e)
    {
        var pcs = _optionTrade?.TradePositions.IntraDay(PutSpreadTradeType, e.EodKey.ValueDate);
        pcs?.SetEndOfDayStatus().SetUpdated(e.UpdatedOn, e.UpdatedBy);

        var ccs = _optionTrade?.TradePositions.IntraDay(CallSpreadTradeType, e.EodKey.ValueDate);
        ccs?.SetEndOfDayStatus().SetUpdated(e.UpdatedOn, e.UpdatedBy);
        return true;
    }

    /// <summary>
    /// delete option trade
    /// </summary>
    /// <param name="e"></param>
    bool On(OptionTradeDeletedEvent e)
    {
        _optionTrade = null!;
        return true;
    }

    /// <summary>
    /// update daily profit target
    /// </summary>
    /// <param name="e"></param>
    bool On(OptionTradeDailyProfitTargetUpdatedEvent e)
    {
        _optionTrade?.TradeLimit.SetDailyProfitTarget(e.DailyProfitTarget);
        return true;
    }

    bool On(OptionTradePositionOpenedEvent e)
    {
        _tradePositionState = e.TradePositionState;
        return true;
    }

    bool On(OptionTradePositionClosedEvent e)
    {
        _tradePositionState = e.TradePositionState;
        return true;
    }

    // public properties...
    IOptionTrade CurrentTrade => _optionTrade ?? throw new InvalidOperationException("Option trade state is not initialized.");

    internal TradeType TradeType => CurrentTrade.TradeType;
    internal DateOnly MaturityDate => CurrentTrade.MaturityDate;
    internal ITradePositionCollection TradePositions => CurrentTrade.TradePositions;
    internal ITradeLimit TradeLimit => CurrentTrade.TradeLimit;
    internal TradePositionState TradePositionState => _tradePositionState;

    internal bool TradeExists(OptionTradeEntityId id) => _optionTrade != null && _optionTrade.Id.Equals(id);
    internal bool TradeDoesNotExist(OptionTradeEntityId id) => !TradeExists(id);
    internal bool CanPlaceBrokerTrade(OptionTradeReadModel optionTrade) => optionTrade.TradeState == TradeState.OrderPlaced || optionTrade.TradeState == TradeState.OrderFilled;
    internal bool CanPlaceManualTrade(OptionTradeReadModel optionTrade) => optionTrade.TradeState == TradeState.TradeToOpen || optionTrade.TradeState == TradeState.TradeToClose;
    internal bool AllTradePositionsInOpenStatus(OptionTradeReadModel optionTrade) => (optionTrade.TradePositions ?? []).All(o => o.EntityId.TradeStatus == TradeStatus.Open);
    internal bool AllTradePositionsInCloseStatus(OptionTradeReadModel optionTrade) => (optionTrade.TradePositions ?? []).All(o => o.EntityId.TradeStatus == TradeStatus.Close);
    internal bool IsTradeInIntraDayStatus(TradeStatus tradeStatus) => tradeStatus == TradeStatus.IntraDay;
    internal bool IsTradeInEndOfDayStatus(TradeStatus tradeStatus) => tradeStatus == TradeStatus.EndOfDay;
    internal bool IsIronCondorTrade(TradeType tradeType)
    {
        switch (tradeType)
        {
            case TradeType.ShortIronCondor:
            case TradeType.LongIronCondor:
                return true;
        }
        return false;
    }
    internal bool TradePositionExists(TradePositionEntityId id)
        => CurrentTrade.TradePositions.Exists(id);

    internal bool HasOptionLegDataChanged(ChangeOptionTradeLegDataCommand e)
    {
        var optionLegData = OptionLegData(e);
        return optionLegData is not null && (optionLegData.BidPrice != e.OptionLegData.BidPrice || optionLegData.AskPrice != e.OptionLegData.AskPrice);
    }

    internal IOptionLegData? OptionLegData(ChangeOptionTradeLegDataCommand e)
    {
        var tradePosition = CurrentTrade.TradePositions.IntraDay(e.TradeType, e.ValueDate);
        return tradePosition switch
        {
            _ when tradePosition is not null => tradePosition.OptionLegData[e.OptionLegData.OptionLegId],
            _ => default
        };
    }

    /// <summary>
    /// return trade position
    /// </summary>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    internal TradePositionReadModel? GetIntraDayTradePosition(TradeType tradeType, DateOnly valueDate)
        => CurrentTrade.TradePositions.IntraDay(tradeType, valueDate)?.ToViewModel() ?? null;

    internal TradePositionEntityId TradePositionId(ChangeOptionTradeLegDataCommand e)
        => new (
            OrderId: e.OrderId,
            TradeId: e.TradeId,
            TradeType: e.TradeType,
            ValueDate: e.ValueDate,
            DaysToExpiry: DaysToExpiry(e.ValueDate),
            TradeStatus: e.TradeStatus);

    internal TradePositionEntityId TradePositionId(ProcessOptionTradeEndOfDayCommand e)
     => new (
         OrderId: e.OrderId,
         TradeId: e.TradeId,
         TradeType: e.TradeType,
         ValueDate: e.ValueDate,
         DaysToExpiry: DaysToExpiry(e.ValueDate),
         TradeStatus: e.TradeStatus);

    /// <summary>
    /// return order quantity
    /// </summary>
    /// <returns></returns>
    internal int GetOrderQuantity()
    {
        var legCount = _optionTrade?.OptionLegs.Count ?? 0;
        return legCount == 0 ? 0 : (_optionTrade?.OptionLegs?.Sum(o => o.Quantity) ?? 0) / legCount;
    }

    internal int GetFilledQuantity() 
        => _optionTrade!.TradeFills.Sum(o => o.FillQuantity) / (_optionTrade?.TradeFills.Count ?? 1);

    internal TradeFillReadModel[] GetTradeFills() 
        => [.. _optionTrade!.TradeFills.Select(o => o.ToViewModel())];

    /// <summary>
    /// return daily profit target
    /// </summary>
    /// <param name="tradingDays"></param>
    /// <param name="maxTradingDays"></param>
    /// <returns></returns>
    internal decimal GetDailyProfitTarget(int tradingDays, int maxTradingDays)
    {
        var tradeCommission = _optionTrade!.TradePositions.Opening().Sum(e => e.Commission);
        return ((_optionTrade.TradeLimit.MaxProfit / maxTradingDays) * tradingDays) + (2 * tradeCommission);
    }

    /// <summary>
    /// return current trade pnl
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    internal decimal GetTradePnl(DateOnly valueDate)
    {
        var tradePosition = _optionTrade!.TradePositions;
        var tradePnl = tradePosition.EndOfDay(PutSpreadTradeType, valueDate)?.TradePnl
            + tradePosition.EndOfDay(CallSpreadTradeType, valueDate)?.TradePnl;
        if (tradePnl is null || tradePnl.Value == 0m)
            tradePnl = tradePosition.IntraDay(PutSpreadTradeType, valueDate)?.TradePnl +
                tradePosition.IntraDay(CallSpreadTradeType, valueDate)?.TradePnl;
        return tradePnl ?? 0m;
    }

    /// <summary>
    /// return trade view model
    /// </summary>
    /// <returns></returns>
    internal OptionTradeReadModel ToOptionTrade() 
        => _optionTrade!.ToReadModel();

    /// <summary>
    /// return current days to expiry
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    internal int DaysToExpiry(DateOnly valueDate) 
        => _optionTrade!.MaturityDate.DayNumber - valueDate.DayNumber;

    /// <summary>
    /// return put spread trade type
    /// </summary>
    /// <returns></returns>
    internal TradeType PutSpreadTradeType
        => _optionTrade!.TradeType switch
        {
            TradeType.ShortIronCondor => TradeType.PutCreditSpread,
            TradeType.LongIronCondor => TradeType.PutDebitSpread,
            _ => TradeType.Unknown
        };

    /// <summary>
    /// return call spread trade type
    /// </summary>
    /// <returns></returns>
    internal TradeType CallSpreadTradeType
        => _optionTrade!.TradeType switch
        {
            TradeType.ShortIronCondor => TradeType.CallCreditSpread,
            TradeType.LongIronCondor => TradeType.CallDebitSpread,
            _ => TradeType.Unknown
        };
}
