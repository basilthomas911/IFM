using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>Deterministic validation shared by lifecycle command handlers.</summary>
public static class TradeValidation
{

    extension(TradeOrderDefinition? order)
    {
        /// <summary>Returns all order definition failures without changing the definition.</summary>
        public string[] Validate() => TradeOrderDefinitionValidation.Validate(order);
    }
}
