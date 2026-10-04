using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies one execution attempt for one exact Trade Order stream.</summary>
[MessagePackObject]
public readonly record struct OrderExecutionId(
    [property: Key(0)] TradeOrderId TradeOrder,
    [property: Key(1)] Guid ExecutionAttemptId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{TradeOrder.Format()}.{ExecutionAttemptId:N}");
    [IgnoreMember] public bool IsValid => TradeOrder.IsValid && ExecutionAttemptId != Guid.Empty;
}

/// <summary>Intrinsic OrderExecutionId validation used by command actors.</summary>
public static class OrderExecutionIdValidationExtensions
{
    /// <summary>Checks the intrinsic business identity without accessing state.</summary>
    public static List<TomasAI.IFM.Shared.Validation.ValidationError> ValidateOrderExecutionId(this List<TomasAI.IFM.Shared.Validation.ValidationError> errors, OrderExecutionId entityId, string commandName)
    {
        if (!entityId.IsValid) errors.Add(new($"{commandName}.EntityId is invalid."));
        return errors;
    }

}
