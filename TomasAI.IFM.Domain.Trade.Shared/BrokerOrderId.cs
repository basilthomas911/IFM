using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies one separately executable component of one order-execution attempt.</summary>
[MessagePackObject]
public readonly record struct BrokerOrderId(
    [property: Key(0)] OrderExecutionId Execution,
    [property: Key(1)] Guid ComponentId) : IActorEntityId
{
    /// <summary>Returns the stable actor stream identifier.</summary>
    public string Format() => string.Create(CultureInfo.InvariantCulture, $"{Execution.Format()}.{ComponentId:N}");
    [IgnoreMember] public bool IsValid => Execution.IsValid && ComponentId != Guid.Empty;
}
