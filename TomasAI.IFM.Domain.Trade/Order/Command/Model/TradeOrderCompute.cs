using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Model;

namespace TomasAI.IFM.Domain.Trade.Order.Command.Model;
/// <summary>An immutable proposed order change; authoritative data remains in command state.</summary>
internal sealed record TradeOrderCompute(bool Accepted, TradeOrderDefinition? TradeOrderDefinition, string RejectionCode, string RejectionReason)
{
    /// <summary>Checks computed domain data before event creation and state application.</summary>
    /// <param name = "tradeOrderId">The command's owning order identity.</param>
    /// <returns>True only for an accepted, valid definition for this order.</returns>
    internal bool IsValidFor(TradeOrderId tradeOrderId) => Accepted && TradeOrderDefinition is { } tradeOrderDefinition && tradeOrderDefinition.Id == tradeOrderId && tradeOrderDefinition.Validate().Length == 0;
    /// <summary>Returns an accepted proposed definition without modifying state.</summary>
    internal static TradeOrderCompute Accept(TradeOrderDefinition tradeOrderDefinition) => new(true, tradeOrderDefinition, "", "");
    /// <summary>Returns a rejected proposal with its business reason.</summary>
    internal static TradeOrderCompute Reject(string rejectionCode, string rejectionReason) => new(false, null, rejectionCode, rejectionReason);
}
