namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

/// <summary>Identifies the strategy position and valuation date shown by the end-of-day screen.</summary>
public sealed class TradeEndOfDayParameter
{
    /// <summary>Gets or sets the Portfolio that owns the trade.</summary>
    public int PortfolioId { get; set; }
    /// <summary>Gets or sets the Fund that owns the trade.</summary>
    public int FundId { get; set; }
    /// <summary>Gets or sets the accepted Trade Order identifier.</summary>
    public int OrderId { get; set; }
    /// <summary>Gets or sets the established Trade identifier.</summary>
    public int TradeId { get; set; }
    /// <summary>Gets or sets the legacy display classification used by existing form labels.</summary>
    public TradeType TradeType { get; set; }
    /// <summary>Gets or sets the strategy actor that owns the position.</summary>
    public TradeStrategyKind StrategyKind { get; set; }
    /// <summary>Gets or sets the stable strategy-position identifier when it is already known.</summary>
    public Guid PositionId { get; set; }
    /// <summary>Gets or sets the broker-neutral underlying contract identifier.</summary>
    public string BaseContractId { get; set; } = string.Empty;
    /// <summary>Gets or sets the valuation date processed by the screen.</summary>
    public DateOnly ValueDate { get; set; }

    /// <summary>Resolves the canonical strategy kind from explicit or legacy input.</summary>
    /// <returns>The supported strategy kind.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the input does not identify a supported strategy.</exception>
    public TradeStrategyKind ResolveStrategyKind() => StrategyKind != TradeStrategyKind.Unknown
        ? StrategyKind
        : TradeType switch
        {
            TradeType.ShortIronCondor or TradeType.LongIronCondor => TradeStrategyKind.IronCondor,
            TradeType.PutCreditSpread or TradeType.PutDebitSpread or
                TradeType.CallCreditSpread or TradeType.CallDebitSpread => TradeStrategyKind.VerticalSpread,
            _ => throw new InvalidOperationException(
                $"End-of-day processing is not mapped for trade type {TradeType}.")
        };

    /// <summary>Builds the complete deterministic position identity used by actor commands and queries.</summary>
    /// <returns>The validated strategy-position identity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the business identity or strategy is invalid.</exception>
    public StrategyPositionId ResolvePositionId()
    {
        var trade = new TradeEntityId(PortfolioId, FundId, OrderId, TradeId);
        if (!trade.IsValid)
            throw new InvalidOperationException(
                "End-of-day processing requires positive Portfolio, Fund, Order, and Trade identifiers.");
        var strategy = ResolveStrategyKind();
        return PositionId == Guid.Empty
            ? StrategyPositionId.Create(trade, strategy)
            : new StrategyPositionId(trade, PositionId);
    }
}
