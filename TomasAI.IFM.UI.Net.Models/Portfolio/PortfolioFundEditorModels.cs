using System.Collections.Immutable;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.UI.Net.Models.Portfolio;

/// <summary>Identifies the lifecycle state displayed for a Portfolio Fund order.</summary>
public enum PortfolioOrderEditorStatus { Open, Cancelled, Completed, Closed }

/// <summary>Identifies a Portfolio Fund order in editor operations.</summary>
public sealed record PortfolioFundOrderEditorId(int FundId, int OrderId);

/// <summary>Identifies a Portfolio Fund order trade in editor operations.</summary>
public sealed record PortfolioFundOrderTradeEditorId(int FundId, int OrderId, int TradeId);

/// <summary>Represents a Portfolio Fund displayed by the trading editor.</summary>
public sealed record PortfolioFundEditorModel
{
    readonly List<PortfolioFundOrderEditorModel> _orders = [];
    /// <summary>Initializes a Portfolio Fund editor model.</summary>
    public PortfolioFundEditorModel(int fundId, string name, string description, decimal balance, bool isProduction, DateTime createdOn, string createdBy)
        => (FundId, Name, Description, Balance, IsProduction, CreatedOn, CreatedBy) = (fundId, name, description, balance, isProduction, createdOn, createdBy);
    public int FundId { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public decimal Balance { get; init; }
    public bool IsProduction { get; init; }
    public DateTime CreatedOn { get; init; }
    public string CreatedBy { get; init; }
    public ImmutableArray<PortfolioFundOrderEditorModel> Orders => [.. _orders];
    /// <summary>Adds an order to this editor model.</summary>
    public void Add(PortfolioFundOrderEditorModel order) { ArgumentNullException.ThrowIfNull(order); _orders.Add(order); }
    /// <summary>Adds orders to this editor model.</summary>
    public void AddRange(IEnumerable<PortfolioFundOrderEditorModel> orders) { ArgumentNullException.ThrowIfNull(orders); _orders.AddRange(orders); }
}

/// <summary>Represents a canonical Portfolio Fund order in editor-friendly form.</summary>
public sealed record PortfolioFundOrderEditorModel
{
    readonly List<PortfolioFundOrderTradeEditorModel> _trades = [];
    /// <summary>Initializes a Portfolio Fund order editor model.</summary>
    public PortfolioFundOrderEditorModel(int fundId, int orderId, DateTime orderDate, PortfolioOrderEditorStatus orderStatus,
        string baseContractId, DateOnly tradeDate, DateOnly maturityDate, string reference, DateTime createdOn,
        string createdBy, DateTime? updatedOn, string updatedBy)
        => (FundId, OrderId, OrderDate, OrderStatus, BaseContractId, TradeDate, MaturityDate, Reference, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
         = (fundId, orderId, orderDate, orderStatus, baseContractId, tradeDate, maturityDate, reference, createdOn, createdBy, updatedOn, updatedBy);
    public int FundId { get; init; }
    public int OrderId { get; init; }
    public DateTime OrderDate { get; init; }
    public PortfolioOrderEditorStatus OrderStatus { get; init; }
    public string BaseContractId { get; init; }
    public DateOnly TradeDate { get; init; }
    public DateOnly MaturityDate { get; init; }
    public string Reference { get; init; }
    public DateTime CreatedOn { get; init; }
    public string CreatedBy { get; init; }
    public DateTime? UpdatedOn { get; init; }
    public string UpdatedBy { get; init; }
    public PortfolioFundOrderEditorId Id => new(FundId, OrderId);
    public ImmutableArray<PortfolioFundOrderTradeEditorModel> Trades => [.. _trades];
    /// <summary>Adds a trade to this editor model.</summary>
    public void Add(PortfolioFundOrderTradeEditorModel trade) { ArgumentNullException.ThrowIfNull(trade); _trades.Add(trade); }
}

/// <summary>Represents a canonical Portfolio Fund order trade in editor-friendly form.</summary>
public sealed record PortfolioFundOrderTradeEditorModel
{
    /// <summary>Initializes an empty editor instance.</summary>
    public PortfolioFundOrderTradeEditorModel() { }
    /// <summary>Initializes a Portfolio Fund order trade editor model.</summary>
    public PortfolioFundOrderTradeEditorModel(int fundId, int orderId, int tradeId, TradeType tradeType, DateOnly tradeDate,
        DateOnly maturityDate, TradeState tradeState, TradeAction tradeAction, string reference, bool primaryTrade,
        string baseContractSymbol, DateTime createdOn, string createdBy, DateTime? updatedOn, string updatedBy, bool? hasFillEvidence = null)
        => (FundId, OrderId, TradeId, TradeType, TradeDate, MaturityDate, TradeState, TradeAction, Reference, PrimaryTrade,
            BaseContractSymbol, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy, HasFillEvidence)
         = (fundId, orderId, tradeId, tradeType, tradeDate, maturityDate, tradeState, tradeAction, reference, primaryTrade,
            baseContractSymbol, createdOn, createdBy, updatedOn, updatedBy, hasFillEvidence);
    public int FundId { get; init; }
    public int OrderId { get; init; }
    public int TradeId { get; init; }
    public TradeType TradeType { get; init; }
    public DateOnly TradeDate { get; init; }
    public DateOnly MaturityDate { get; init; }
    public TradeState TradeState { get; init; }
    public TradeAction TradeAction { get; init; }
    public string Reference { get; init; } = string.Empty;
    public bool PrimaryTrade { get; init; }
    public string BaseContractSymbol { get; init; } = string.Empty;
    public DateTime CreatedOn { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedOn { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
    public bool? HasFillEvidence { get; init; }
    public PortfolioFundOrderTradeEditorId Id => new(FundId, OrderId, TradeId);
    /// <summary>Gets underlying contract identifiers encoded by the reference.</summary>
    public string[] GetContractIds()
    {
        if (string.IsNullOrWhiteSpace(Reference)) return [];
        if (TradeType == TradeType.FuturesOutright) return [Reference.Trim()];
        return Reference.Trim().ToUpperInvariant().Split('X', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).SelectMany(ParseLeg).ToArray();
    }
    IEnumerable<string> ParseLeg(string leg)
    {
        var type = leg.FirstOrDefault(character => character is 'P' or 'C');
        if (type == default) return [];
        return leg[(leg.IndexOf(type) + 1)..].Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(strike => $"{BaseContractSymbol.Trim()}{MaturityDate:yyyyMMdd}{type}{strike}");
    }
}

/// <summary>Defines editor permissions for canonical Portfolio Fund orders.</summary>
public static class PortfolioFundOrderEditorPolicy
{
    public const int MaximumTradeCount = 2;
    /// <summary>Returns whether the trade has economic evidence that prevents deletion.</summary>
    public static bool HasEconomicEvidence(PortfolioFundOrderTradeEditorModel trade) => trade.TradeState switch
    { TradeState.NewTrade => false, TradeState.OrderCancelled => trade.HasFillEvidence is not false, _ => true };
    /// <summary>Returns whether a trade may be removed.</summary>
    public static bool CanRemoveTrade(PortfolioFundOrderEditorModel order, PortfolioFundOrderTradeEditorModel trade)
        => order.OrderStatus == PortfolioOrderEditorStatus.Open && order.Trades.Length < MaximumTradeCount && !HasEconomicEvidence(trade)
           && order.Trades.All(candidate => candidate.TradeState is not (TradeState.TradeToClose or TradeState.OrderCompleted));
    /// <summary>Returns whether an economically inactive order may be deleted.</summary>
    public static bool CanDeleteOrder(PortfolioFundOrderEditorModel order)
        => order.OrderStatus == PortfolioOrderEditorStatus.Open && order.Trades.All(trade => CanRemoveTrade(order, trade));
    /// <summary>Returns whether another trade may be added.</summary>
    public static bool CanAddTrade(PortfolioFundOrderEditorModel order)
    {
        if (order.OrderStatus != PortfolioOrderEditorStatus.Open || order.Trades.Length >= MaximumTradeCount) return false;
        if (order.Trades.Length == 0) return true;
        var opening = order.Trades.SingleOrDefault(trade => trade.PrimaryTrade);
        return opening is not null && opening.TradeState == TradeState.TradeToOpen;
    }
    /// <summary>Returns whether the order may be closed.</summary>
    public static bool CanCloseOrder(PortfolioFundOrderEditorModel order)
        => order.OrderStatus == PortfolioOrderEditorStatus.Open && order.Trades.Length == MaximumTradeCount
           && order.Trades.Count(trade => trade.PrimaryTrade) == 1
           && order.Trades.Any(trade => !trade.PrimaryTrade && trade.TradeState == TradeState.OrderCompleted);
    /// <summary>Returns the closing trade type for an opening trade type.</summary>
    public static TradeType ClosingType(TradeType type) => type switch
    {
        TradeType.ShortIronCondor => TradeType.LongIronCondor, TradeType.LongIronCondor => TradeType.ShortIronCondor,
        TradeType.PutCreditSpread => TradeType.PutDebitSpread, TradeType.PutDebitSpread => TradeType.PutCreditSpread,
        TradeType.CallCreditSpread => TradeType.CallDebitSpread, TradeType.CallDebitSpread => TradeType.CallCreditSpread,
        TradeType.FuturesOutright => TradeType.FuturesOutright, _ => TradeType.Unknown
    };
}