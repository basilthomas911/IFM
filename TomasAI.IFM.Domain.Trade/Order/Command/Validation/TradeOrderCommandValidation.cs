using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Command.Validation;

/// <summary>Deterministic Trade Order ingress validation before state loading.</summary>
public static class TradeOrderCommandValidation
{
    public static List<ValidationError> Validate(ICommand command) => new List<ValidationError>()
        .ValidateCommandId(command.CommandId, command.CommandName)
        .CaptureCommandValidation(() =>
        {
            if (command is not ICommand<TradeOrderId> typed || !typed.EntityId.IsValid ||
                !string.Equals(command.Subject.EntityId, typed.EntityId.Format(), StringComparison.Ordinal))
                throw new ArgumentException("Valid Trade Order identity and matching subject are required.");
        });
}
