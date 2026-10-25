using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Intrinsic TradeEntityId validation used by command actors.</summary>
public static class TradeEntityIdValidationExtensions
{
    /// <summary>Validates all four business identifiers of a Trade.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<TomasAI.IFM.Shared.Validation.ValidationError> ValidateTradeEntityId(this List<TomasAI.IFM.Shared.Validation.ValidationError> errors, TradeEntityId id, string commandName)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!id.IsValid) errors.Add(new($"{commandName}.EntityId requires positive Portfolio, Fund, Order and Trade IDs."));
        return errors;
    }

}
