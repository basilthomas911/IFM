using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
[MessagePackObject]
public sealed record OrderCompositionProjection([property: Key(0)] OrderCompositionFunctionCompletedEvent Completion, [property: Key(1)] bool WorkflowAccepted, [property: Key(2)] bool AcceptanceUnknown, [property: Key(3)] bool SuspectedOrphan);
