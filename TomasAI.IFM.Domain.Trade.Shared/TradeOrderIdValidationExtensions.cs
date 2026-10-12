using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Intrinsic TradeOrderId validation used by command actors.</summary>
public static class TradeOrderIdValidationExtensions
{
    /// <summary>Checks the intrinsic business identity without accessing state.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<TomasAI.IFM.Shared.Validation.ValidationError> ValidateTradeOrderId(this List<TomasAI.IFM.Shared.Validation.ValidationError> errors, TradeOrderId entityId, string commandName)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!entityId.IsValid) errors.Add(new($"{commandName}.EntityId is invalid."));
        return errors;
    }

}
