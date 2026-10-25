using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

[MessagePackObject] public sealed record CloseIronCondorPositionCommand : TimedPositionCommand { public const string Verb = "CloseIronCondorPosition"; private ExecutionFillEvidence[] closingFills = []; [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
[Key(5)] public ExecutionFillEvidence[] ClosingFills { get => closingFills; init => closingFills = value ?? []; } [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }
