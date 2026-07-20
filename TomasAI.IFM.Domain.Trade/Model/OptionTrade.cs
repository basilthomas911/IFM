using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Domain.Trade.Model.Strategy;

namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>
/// create option trade
/// </summary>
/// <param name="orderId"></param>
/// <param name="tradeId"></param>
/// <param name="tradeStrategy"></param>
/// <param name="tradeDate"></param>
/// <param name="maturityDate"></param>
/// <param name="tradeType"></param>
/// <param name="tradeState"></param>
/// <param name="tradeAction"></param>
/// <param name="underlyingContractId"></param>
/// <param name="underlyingAssetType"></param>
/// <param name="isPrimaryTrade"></param>
/// <param name="isHedgeTrade"></param>
/// <param name="createdOn"></param>
/// <param name="createdBy"></param>
/// <param name="updatedOn"></param>
/// <param name="updatedBy"></param>
public class OptionTrade(
    int orderId,
    int tradeId,
    string tradeStrategy,
    DateOnly tradeDate,
    DateOnly maturityDate,
    TradeType tradeType,
    TradeState tradeState,
    TradeAction tradeAction,
    string underlyingContractId,
    AssetType underlyingAssetType,
    bool isPrimaryTrade,
    bool isHedgeTrade,
    DateTime createdOn,
    string createdBy,
    DateTime updatedOn,
    string updatedBy) : AbstractTrade(orderId, tradeId, tradeStrategy, tradeDate, maturityDate, tradeType, tradeState, tradeAction, underlyingContractId, underlyingAssetType, isPrimaryTrade, isHedgeTrade), IOptionTrade
{
    readonly OptionTradeEntityId _id = new(orderId, tradeId);
    readonly IOptionLegCollection _optionLegs = new OptionLegCollection(tradeId);
    readonly ITradePositionCollection _tradePositions = new TradePositionCollection();
    ITradeLimit? _tradeLimit;
    readonly TradeTypeLimitCollection _tradeTypeLimits = new(tradeId);
    readonly TradeFillCollection _tradeFills = [];
    readonly DateTime _createdOn = createdOn;
    readonly string _createdBy = createdBy;
    DateTime _updatedOn = updatedOn;
    string _updatedBy = updatedBy;

    public static OptionTrade Create(
        int orderId,
        int tradeId,
        string tradeStrategy,
        DateOnly tradeDate,
        DateOnly maturityDate,
        TradeType tradeType,
        TradeState tradeState,
        TradeAction tradeAction,
        string underlyingContractId,
        AssetType underlyingAssetType,
        bool isPrimaryTrade,
        bool isHedgeTrade,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    {
        OptionTrade? optionTrade = tradeType switch
        {
            _ => new IronCondorTrade(orderId, tradeId, tradeStrategy, tradeDate, maturityDate, tradeType, tradeState, tradeAction, underlyingContractId, underlyingAssetType, isPrimaryTrade, isHedgeTrade, createdOn, createdBy, updatedOn, updatedBy),
        };
        return optionTrade;
    }

    public OptionTradeEntityId Id => _id;
    public IOptionLegCollection OptionLegs => _optionLegs;
    public ITradePositionCollection TradePositions => _tradePositions;
    public ITradeLimit TradeLimit => _tradeLimit ?? throw new InvalidOperationException("TradeLimit has not been initialized.");
    public ITradeTypeLimitCollection TradeTypeLimits => _tradeTypeLimits;
    public ITradeFillCollection TradeFills => _tradeFills;
    public DateTime CreatedOn => _createdOn;
    public string CreatedBy => _createdBy;
    public DateTime UpdatedOn => _updatedOn;
    public string UpdatedBy => _updatedBy;

    /// <summary>
    /// create option trade from option trade view model
    /// </summary>
    /// <param name="e"></param>
    public OptionTrade(OptionTradeReadModel e)
        : this(
        orderId: e.OrderId,
        tradeId: e.TradeId,
        tradeStrategy: e.TradeStrategy,
        tradeDate: e.TradeDate,
        maturityDate: e.MaturityDate,
        tradeType: e.TradeType,
        tradeState: e.TradeState,
        tradeAction: e.TradeAction,
        underlyingContractId: e.UnderlyingContractId,
        underlyingAssetType: e.UnderlyingAssetType,
        isPrimaryTrade: e.IsPrimaryTrade,
        isHedgeTrade: e.IsHedgeTrade,
        createdOn: e.CreatedOn,
        createdBy: e.CreatedBy,
        updatedOn: e.UpdatedOn,
        updatedBy: e.UpdatedBy)
    {
        foreach (var o in e.OptionLegs!)
            _optionLegs.Add(new OptionLeg(
                orderId: e.OrderId,
                tradeId: e.TradeId,
                contractId: o.ContractId,
                quantity: o.Quantity,
                strikePrice: o.StrikePrice,
                optionLegType: o.OptionLegType,
                optionLegAction: o.OptionLegAction,
                createdOn: e.CreatedOn,
                createdBy: e.CreatedBy,
                updatedOn: e.UpdatedOn,
                updatedBy: e.UpdatedBy));
        foreach (var o in e.TradePositions!)
            _tradePositions.Add(new TradePosition(o, e.CreatedOn, e.CreatedBy));
        if (e.TradeLimit is null)
            throw new InvalidOperationException("Option trade read model is missing TradeLimit.");
        _tradeLimit = new TradeLimit(e.TradeLimit, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy);
        foreach (var o in e.TradeTypeLimits!)
            _tradeTypeLimits.Add(new TradeTypeLimit(o.TradeId, o.TradeType, o.MaxLossLimit, o.MinProfitLimit, o.MaxProfitLimit));
        if (e.TradeFills != null)
            foreach (var o in e.TradeFills)
                _tradeFills.Add(new TradeFill(o));
    }

 
    /// <summary>
    /// return trade pnl
    /// </summary>
    /// <returns></returns>
    public virtual decimal GetTradePnl() => 0m;

    /// <summary>
    /// return loss probability
    /// </summary>
    /// <returns></returns>
    public virtual double GetLossProbability() => 0.0;

    /// <summary>
    /// return short put probability
    /// </summary>
    /// <param name="assetPrice"></param>
    /// <returns></returns>
    public double GetShortPutProbability(double assetPrice)
    {
        var optionLeg = _optionLegs[OptionLegAction.Short, OptionType.Put];
        var pcs = _tradePositions.IntraDay(TradeType.PutCreditSpread);
        if (pcs is null)
        {
            pcs = _tradePositions.EndOfDay(TradeType.PutCreditSpread);
            pcs ??= _tradePositions.Opening(TradeType.PutCreditSpread);
            if (pcs is null) return 0.0;
        }
        var optionLegData = pcs.OptionLegData[optionLeg?.ContractId ?? string.Empty];
        return optionLegData?.GetOTMProbability(assetPrice) ?? 0;
    }

    /// <summary>
    /// return short call probability
    /// </summary>
    /// <param name="assetPrice"></param>
    /// <returns></returns>
    public double GetShortCallProbability(double assetPrice)
    {
        var optionLeg = _optionLegs[OptionLegAction.Short, OptionType.Call];
        var ccs = _tradePositions.IntraDay(TradeType.CallCreditSpread);
        if (ccs is null)
        {
            ccs = _tradePositions.EndOfDay(TradeType.CallCreditSpread);
            ccs ??= _tradePositions.Opening(TradeType.CallCreditSpread);
            if (ccs is null) return 0.0;
        }
        var optionLegData = ccs.OptionLegData[optionLeg?.ContractId ?? string.Empty];
        return optionLegData?.GetOTMProbability(assetPrice) ?? 0;
    }

    /// <summary>
    /// add option leg
    /// </summary>
    /// <param name="optionLeg"></param>
    public void AddOptionLeg(IOptionLeg optionLeg)
    {
        if (optionLeg is null)
            throw new ArgumentNullException(nameof(optionLeg), "OptionTrade.AddOptionLeg: optionLeg parameter is null");
        if (_optionLegs.Exists(optionLeg.ContractId))
            throw new InvalidOperationException($"OptionTrade.AddOptionLeg: optionLeg {optionLeg.ContractId} already exists in trade {optionLeg.TradeId} ");
        _optionLegs.Add(optionLeg);
    }

    /// <summary>
    /// add option leg
    /// </summary>
    /// <param name="optionLeg"></param>
    public void AddOptionLegs(ICollection<IOptionLeg> optionLegs)
    {
        foreach (var ol in optionLegs)
        {
            if (!_optionLegs.Exists(ol.ContractId))
                _optionLegs.Add(ol);
        }
    }

    /// <summary>
    /// add tradePosition
    /// </summary>
    /// <param name="tradePosition"></param>
    public void AddTradePosition(ITradePosition tradePosition)
    {
        if (!_tradePositions.Exists(tradePosition.Id))
            _tradePositions.Add(tradePosition);
    }

    /// <summary>
    /// add trade positions
    /// </summary>
    /// <param name="tradePositions"></param>
    public void AddTradePositions(ICollection<ITradePosition> tradePositions)
    {
        foreach (var e in tradePositions)
            if (!_tradePositions.Exists(e.Id))
                _tradePositions.Add(e);
    }

    /// <summary>
    /// add trade type limits
    /// </summary>
    /// <param name="tradeTypeLimits"></param>
    public void AddTradeTypeLimits(ICollection<ITradeTypeLimit> tradeTypeLimits)
    {
        if (tradeTypeLimits.Count > 0)
            foreach (var e in tradeTypeLimits)
                _tradeTypeLimits.Add(e);
    }

    /// <summary>
    /// add trade fills
    /// </summary>
    /// <param name="tradeFills"></param>
    /// <param name="createdOn"></param>
    /// <param name="createdBy"></param>
    public void AddTradeFills(ICollection<ITradeFill> tradeFills, DateTime createdOn, string createdBy)
    {
        if (tradeFills.Count > 0)
            _tradeFills.Add(tradeFills);
    }

    /// <summary>
    /// set trade limit
    /// </summary>
    /// <param name="tradeLimit"></param>
    /// <returns></returns>
    public IOptionTrade SetTradeLimit(ITradeLimit tradeLimit)
    {
        _tradeLimit = tradeLimit;
        return this;
    }

    /// <summary>
    /// change trade state
    /// </summary>
    /// <param name="tradeState"></param>
    /// <returns></returns>
    public new IOptionTrade SetTradeState(TradeState tradeState)
    {
        base.SetTradeState(tradeState);
        return this;
    }

    /// <summary>
    /// change updated info
    /// </summary>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    public IOptionTrade SetUpdated(DateTime updatedOn, string updatedBy)
    {
        _updatedOn = updatedOn;
        _updatedBy = updatedBy;
        return this;
    }

    /// <summary>
    /// return view model of option trade
    /// </summary>
    /// <returns></returns>
    public OptionTradeReadModel ToReadModel()
        => new OptionTradeReadModel(
            orderId: OrderId,
            tradeId: TradeId,
            tradeStrategy: TradeStrategy,
            tradeDate: TradeDate, 
            maturityDate: MaturityDate,
            tradeType: TradeType,
            tradeState: TradeState,
            tradeAction: TradeAction,
            underlyingContractId: UnderlyingContractId,
            underlyingAssetType: UnderlyingAssetType,
            isPrimaryTrade: IsPrimaryTrade,
            isHedgeTrade: IsHedgeTrade,
            createdOn: CreatedOn,
            createdBy: CreatedBy,
            updatedOn: UpdatedOn,
            updatedBy: UpdatedBy
        ).AddOptionLegs([.. OptionLegs.Select(e => e.ToDataModel())])
        .AddTradePosition([.. TradePositions.Select(e => e.ToViewModel())])
        .SetTradeLimit(TradeLimit.ToViewModel())
        .AddTradeTypeLimits([.. TradeTypeLimits.Select(e => e.ToViewModel())])
        .AddTradeFills([.. TradeFills.Select(e => e.ToViewModel())]);
}
