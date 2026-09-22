using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
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

/// <summary>Captures the operator inputs used to reserve a new manual Portfolio Fund order.</summary>
public sealed record ManualFundOrderDraftEditorModel(
    int FundId,
    int OrderId,
    DateTime OrderDate,
    PortfolioOrderEditorStatus OrderStatus,
    string BaseContractId,
    DateOnly TradeDate,
    DateOnly MaturityDate,
    string Reference,
    DateTime CreatedOn,
    string CreatedBy,
    DateTime? UpdatedOn,
    string UpdatedBy);

/// <summary>Represents the canonical Portfolio Fund order lifecycle in editor-friendly form.</summary>
public sealed record PortfolioFundOrderEditorModel
{
    readonly List<PortfolioFundOrderTradeEditorModel> _trades = [];
    /// <summary>Initializes the UI order from its canonical projection without copying trade-owned fields.</summary>
    public PortfolioFundOrderEditorModel(FundOrderProjectionReadModel order)
    {
        ArgumentNullException.ThrowIfNull(order);
        PortfolioId = order.PortfolioId;
        FundId = order.FundId;
        OrderId = order.OrderId;
        WorkflowId = order.WorkflowId;
        Status = order.Status;
        CreatedOnUtc = order.CreatedOnUtc;
        CreatedBy = order.CreatedBy;
        CompositionResultId = order.CompositionResultId;
        CompositionResultHash = order.CompositionResultHash;
        AggregateVersion = order.AggregateVersion;
        WorkflowRevision = order.WorkflowRevision;
        TradeSelectionResultId = order.TradeSelectionResultId;
        TradeSelectionResultHash = order.TradeSelectionResultHash;
        TradeTemplateId = order.TradeTemplateId;
        TradeTemplateVersion = order.TradeTemplateVersion;
        OrderCompositionProfileId = order.OrderCompositionProfileId;
        OrderCompositionProfileVersion = order.OrderCompositionProfileVersion;
        StrategySnapshotHash = order.StrategySnapshotHash;
        ExpiresAtUtc = order.ExpiresAtUtc;
        RiskResultId = order.RiskResultId;
        RiskResultHash = order.RiskResultHash;
        StopReason = order.StopReason;
        IdempotencyKey = order.IdempotencyKey;
        CanonicalRequestHash = order.CanonicalRequestHash;
        Origin = order.Origin;
        OperatorReference = order.OperatorReference;
        RiskAuthorization = order.RiskAuthorization;
        TerminalRisk = order.TerminalRisk;
    }
    public int PortfolioId { get; init; }
    public int FundId { get; init; }
    public int OrderId { get; init; }
    public Guid WorkflowId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedOnUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public Guid CompositionResultId { get; init; }
    public string CompositionResultHash { get; init; } = string.Empty;
    public long AggregateVersion { get; init; }
    public long WorkflowRevision { get; init; }
    public Guid TradeSelectionResultId { get; init; }
    public string TradeSelectionResultHash { get; init; } = string.Empty;
    public Guid TradeTemplateId { get; init; }
    public long TradeTemplateVersion { get; init; }
    public Guid OrderCompositionProfileId { get; init; }
    public long OrderCompositionProfileVersion { get; init; }
    public string StrategySnapshotHash { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
    public Guid RiskResultId { get; init; }
    public string RiskResultHash { get; init; } = string.Empty;
    public string StopReason { get; init; } = string.Empty;
    public Guid IdempotencyKey { get; init; }
    public string CanonicalRequestHash { get; init; } = string.Empty;
    public CompositionOrigin Origin { get; init; }
    public string OperatorReference { get; init; } = string.Empty;
    public FundRiskAuthorizationReference? RiskAuthorization { get; init; }
    public RiskTerminalEvidence? TerminalRisk { get; init; }
    public PortfolioFundOrderEditorId Id => new(FundId, OrderId);
    public ImmutableArray<PortfolioFundOrderTradeEditorModel> Trades => [.. _trades];
    /// <summary>Adds a trade to this editor model.</summary>
    public void Add(PortfolioFundOrderTradeEditorModel trade) { ArgumentNullException.ThrowIfNull(trade); _trades.Add(trade); }
}

/// <summary>Represents a canonical Portfolio Fund order trade in editor-friendly form.</summary>
public sealed record PortfolioFundOrderTradeEditorModel
{
    public int PortfolioId { get; init; }
    public int FundId { get; init; }
    public int OrderId { get; init; }
    public int TradeId { get; init; }
    public string TradeFamily { get; init; } = string.Empty;
    public string InstructionReference { get; init; } = string.Empty;
    public int LegOrdinal { get; init; }
    public long AggregateVersion { get; init; }
    public string DirectionOrBias { get; init; } = string.Empty;
    public TradeAction TradeAction { get; init; }
    public string UnderlyingRoot { get; init; } = string.Empty;
    public DateOnly RequestedTradeDate { get; init; }
    public DateOnly? RequestedMaturityDate { get; init; }
    public TradeType TradeType { get; init; }
    public TradeState TradeState { get; init; }
    public bool PrimaryTrade { get; init; }
    public string BaseContractSymbol { get; init; } = string.Empty;
    /// <summary>The concrete futures contract selected by the UI for pricing and execution.</summary>
    public string BaseContractId { get; init; } = string.Empty;
    public DateTime CreatedOnUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public bool? HasFillEvidence { get; init; }
    public PortfolioFundOrderTradeEditorId Id => new(FundId, OrderId, TradeId);
    /// <summary>Gets underlying contract identifiers encoded by the reference.</summary>
    public string[] GetContractIds()
    {
        if (string.IsNullOrWhiteSpace(InstructionReference)) return [];
        if (TradeType == TradeType.FuturesOutright) return [InstructionReference.Trim()];
        return InstructionReference.Trim().ToUpperInvariant().Split('X', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).SelectMany(ParseLeg).ToArray();
    }
    IEnumerable<string> ParseLeg(string leg)
    {
        var type = leg.FirstOrDefault(character => character is 'P' or 'C');
        if (type == default) return [];
        return leg[(leg.IndexOf(type) + 1)..].Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(strike => $"{BaseContractSymbol.Trim()}{(RequestedMaturityDate ?? RequestedTradeDate):yyyyMMdd}{type}{strike}");
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
        => order.Status == nameof(FundCompositionState.Draft) && order.Trades.Length < MaximumTradeCount && !HasEconomicEvidence(trade)
           && order.Trades.All(candidate => candidate.TradeState is not (TradeState.TradeToClose or TradeState.OrderCompleted));
    /// <summary>Returns whether an economically inactive order may be deleted.</summary>
    public static bool CanDeleteOrder(PortfolioFundOrderEditorModel order)
        => order.Status == nameof(FundCompositionState.Draft) && order.Trades.All(trade => CanRemoveTrade(order, trade));
    /// <summary>Returns whether another trade may be added.</summary>
    public static bool CanAddTrade(PortfolioFundOrderEditorModel order)
    {
        if (order.Status != nameof(FundCompositionState.Draft) || order.Trades.Length >= MaximumTradeCount) return false;
        if (order.Trades.Length == 0) return true;
        var opening = order.Trades.SingleOrDefault(trade => trade.PrimaryTrade);
        return opening is not null && opening.TradeState == TradeState.TradeToOpen;
    }
    /// <summary>Returns whether the order may be closed.</summary>
    public static bool CanCloseOrder(PortfolioFundOrderEditorModel order)
        => order.Status == nameof(FundCompositionState.Draft) && order.Trades.Length == MaximumTradeCount
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
