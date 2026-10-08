using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.Model;

/// <summary>Immutable proposed position snapshot or a rejected position transition.</summary>
internal sealed record PositionChange(bool Accepted, StrategyPositionSnapshot? PositionSnapshot, string RejectionCode, string RejectionReason);
