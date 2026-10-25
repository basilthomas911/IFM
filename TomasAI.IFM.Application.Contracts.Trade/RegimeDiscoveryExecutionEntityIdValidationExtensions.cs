using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Adapts the intrinsic execution identity rules to actor validation.</summary>
public static class RegimeDiscoveryExecutionEntityIdValidationExtensions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateRegimeDiscoveryExecutionEntityId(
        this List<ValidationError> errors, RegimeDiscoveryExecutionEntityId value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        errors.AddRange(new RegimeDiscoveryExecutionEntityIdValidationRules().Execute(value));
        return errors;
    }
}
