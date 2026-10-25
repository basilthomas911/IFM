using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Trade.Shared;
/// <summary>Immutable monitoring limits derived from the Portfolio approval and accepted opening fills.</summary>
[MessagePackObject]
public sealed record EstablishedTradeMonitoringLimits
{
    [Key(0)] public Guid PortfolioApprovalId { get; init; }
    [Key(1)] public int OrderRevision { get; init; }
    [Key(2)] public string DefinitionHash { get; init; } = string.Empty;
    [Key(3)] public TradeLimitReadModel TradeLimit { get; init; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Immutable serialized source-event snapshot.")]
    [Key(4)] public TradeTypeLimitReadModel[] SpreadLimits { get; init; } = [];
}
