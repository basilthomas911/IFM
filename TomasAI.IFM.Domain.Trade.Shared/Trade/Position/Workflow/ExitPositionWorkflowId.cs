using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct ExitPositionWorkflowId(
    [property: Key(0)] StrategyPositionId Position,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] Guid ExitDecisionId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{Position.Format()}.{ValueDate:yyyyMMdd}.{ExitDecisionId:N}");
    [IgnoreMember] public bool IsValid => Position.IsValid && ValueDate != default && ExitDecisionId != Guid.Empty;
}
