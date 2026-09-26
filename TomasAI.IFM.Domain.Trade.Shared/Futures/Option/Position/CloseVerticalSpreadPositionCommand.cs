using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
[MessagePackObject] public sealed record CloseVerticalSpreadPositionCommand : TimedPositionCommand { public const string Verb="CloseVerticalSpreadPosition"; private ExecutionFillEvidence[] closingFills=[]; [Key(5)] public ExecutionFillEvidence[] ClosingFills { get=>closingFills; init=>closingFills=value??[]; } [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext; }
