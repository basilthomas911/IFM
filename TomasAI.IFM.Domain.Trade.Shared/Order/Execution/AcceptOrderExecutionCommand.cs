using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
[MessagePackObject]
public sealed record AcceptOrderExecutionCommand : TimedOrderExecutionCommand { public const string Verb = "AcceptOrderExecution"; }
