using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
[MessagePackObject]
public sealed record TradeSelectionProjection([property: Key(0)] TradeSelectionFunctionCompletedEvent Completion, [property: Key(1)] bool WorkflowAccepted, [property: Key(2)] bool AcceptanceUnknown, [property: Key(3)] bool SuspectedOrphan);
