using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>Deterministic validation shared by lifecycle command handlers.</summary>
public static class TradeValidation
{
    /// <summary>Returns all order definition failures without changing the definition.</summary>
    public static string[] Validate(this TradeOrderDefinition? order) => TradeOrderDefinitionValidation.Validate(order);
}
