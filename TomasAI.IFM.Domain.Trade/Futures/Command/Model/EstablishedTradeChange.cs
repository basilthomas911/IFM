using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.Domain.Trade.Futures.Command.Model;
/// <summary>Immutable established trade proposal and its initial-establishment semantics.</summary>
internal sealed record EstablishedTradeChange(EstablishedTradeDefinition? EstablishedTradeDefinition, bool IsInitialEstablishment = false, string? RejectionReason = null);
