using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Intrinsic StrategyPositionId validation used by command actors.</summary>
public static class StrategyPositionIdValidationExtensions
{
    /// <summary>Validates the Trade identity and strategy Position identity.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<TomasAI.IFM.Shared.Validation.ValidationError> ValidateStrategyPositionId(this List<TomasAI.IFM.Shared.Validation.ValidationError> errors, StrategyPositionId id, string commandName)
    {
        ArgumentNullException.ThrowIfNull(errors);
        errors.ValidateTradeEntityId(id.Trade, commandName);
        if (id.PositionId == Guid.Empty) errors.Add(new($"{commandName}.EntityId.PositionId is required."));
        return errors;
    }

}
