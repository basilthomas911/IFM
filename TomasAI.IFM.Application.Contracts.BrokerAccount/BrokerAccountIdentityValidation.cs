using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Intrinsic broker account identity validation.</summary>
public static class BrokerAccountIdentityValidation
{
    /// <summary>Checks the intrinsic business identity without accessing state.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published broker message storage width and collection contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published broker message storage width and collection contract.")]
    public static List<TomasAI.IFM.Shared.Validation.ValidationError> ValidateBrokerAccountId(this List<TomasAI.IFM.Shared.Validation.ValidationError> errors, BrokerAccountId entityId, string commandName)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!entityId.IsValid) errors.Add(new($"{commandName}.EntityId is invalid."));
        return errors;
    }

}
