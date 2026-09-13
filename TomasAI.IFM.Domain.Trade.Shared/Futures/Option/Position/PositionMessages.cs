using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

public static class PositionActorNames
{
    public const string IronCondorCommand = "FuturesIronCondorTradePositionCommand";
    public const string VerticalSpreadCommand = "FuturesVerticalSpreadTradePositionCommand";
    public const string Query = "FuturesOptionTradePositionQuery";
}

[MessagePackObject]
[Union(0, typeof(OpenIronCondorPositionCommand))]
[Union(1, typeof(UpdateIronCondorPositionLegMarketPriceCommand))]
[Union(2, typeof(EndOfDayIronCondorPositionCommand))]
[Union(3, typeof(CloseIronCondorPositionCommand))]
[Union(4, typeof(CorrectIronCondorPositionBasisCommand))]
[Union(5, typeof(SnapshotIronCondorPositionCommand))]
[Union(6, typeof(OpenVerticalSpreadPositionCommand))]
[Union(7, typeof(UpdateVerticalSpreadPositionLegMarketPriceCommand))]
[Union(8, typeof(EndOfDayVerticalSpreadPositionCommand))]
[Union(9, typeof(CloseVerticalSpreadPositionCommand))]
[Union(10, typeof(CorrectVerticalSpreadPositionBasisCommand))]
[Union(11, typeof(SnapshotVerticalSpreadPositionCommand))]
public abstract record PositionCommand : ICommand<StrategyPositionId>
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public StrategyPositionId EntityId { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public abstract BoundedContextName RouteTo { get; }
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Subject.Name}Actor";
    [IgnoreMember] public int ErrorCode => 25104;
}

[MessagePackObject]
[Union(0, typeof(OpenIronCondorPositionCommand))]
[Union(1, typeof(OpenVerticalSpreadPositionCommand))]
public abstract record OpenPositionCommand : PositionCommand
{
    [Key(4)] public EstablishedTradeDefinition Trade { get; init; } = new();
    [Key(5)] public DateTime EffectiveAtUtc { get; init; }
}
[MessagePackObject]
[Union(0, typeof(UpdateIronCondorPositionLegMarketPriceCommand))]
[Union(1, typeof(UpdateVerticalSpreadPositionLegMarketPriceCommand))]
public abstract record UpdatePositionLegMarketPriceCommand : PositionCommand
{
    [Key(4)] public Guid TradeLegId { get; init; }
    [Key(5)] public decimal Price { get; init; }
    [Key(6)] public long SourceSequence { get; init; }
    [Key(7)] public long RouteGeneration { get; init; }
    [Key(8)] public DateTime EffectiveAtUtc { get; init; }
}
[MessagePackObject]
[Union(0, typeof(EndOfDayIronCondorPositionCommand))]
[Union(1, typeof(CloseIronCondorPositionCommand))]
[Union(2, typeof(EndOfDayVerticalSpreadPositionCommand))]
[Union(3, typeof(CloseVerticalSpreadPositionCommand))]
public abstract record TimedPositionCommand : PositionCommand
{ [Key(4)] public DateTime EffectiveAtUtc { get; init; } }
[MessagePackObject]
[Union(0, typeof(CorrectIronCondorPositionBasisCommand))]
[Union(1, typeof(CorrectVerticalSpreadPositionBasisCommand))]
public abstract record CorrectPositionBasisCommand : PositionCommand
{
    [Key(4)] public Guid TradeLegId { get; init; }
    [Key(5)] public decimal Price { get; init; }
    [Key(6)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject] public sealed record OpenIronCondorPositionCommand : OpenPositionCommand { public const string Verb="OpenIronCondorPosition"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }
[MessagePackObject] public sealed record UpdateIronCondorPositionLegMarketPriceCommand : UpdatePositionLegMarketPriceCommand { public const string Verb="UpdateIronCondorPositionLegMarketPrice"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }
[MessagePackObject] public sealed record EndOfDayIronCondorPositionCommand : TimedPositionCommand { public const string Verb="EndOfDayIronCondorPosition"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }
[MessagePackObject] public sealed record CloseIronCondorPositionCommand : TimedPositionCommand { public const string Verb="CloseIronCondorPosition"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }
[MessagePackObject] public sealed record CorrectIronCondorPositionBasisCommand : CorrectPositionBasisCommand { public const string Verb="CorrectIronCondorPositionBasis"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }
[MessagePackObject] public sealed record SnapshotIronCondorPositionCommand : PositionCommand { public const string Verb="SnapshotIronCondorPosition"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }

[MessagePackObject] public sealed record OpenVerticalSpreadPositionCommand : OpenPositionCommand { public const string Verb="OpenVerticalSpreadPosition"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext; }
[MessagePackObject] public sealed record UpdateVerticalSpreadPositionLegMarketPriceCommand : UpdatePositionLegMarketPriceCommand { public const string Verb="UpdateVerticalSpreadPositionLegMarketPrice"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext; }
[MessagePackObject] public sealed record EndOfDayVerticalSpreadPositionCommand : TimedPositionCommand { public const string Verb="EndOfDayVerticalSpreadPosition"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext; }
[MessagePackObject] public sealed record CloseVerticalSpreadPositionCommand : TimedPositionCommand { public const string Verb="CloseVerticalSpreadPosition"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext; }
[MessagePackObject] public sealed record CorrectVerticalSpreadPositionBasisCommand : CorrectPositionBasisCommand { public const string Verb="CorrectVerticalSpreadPositionBasis"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext; }
[MessagePackObject] public sealed record SnapshotVerticalSpreadPositionCommand : PositionCommand { public const string Verb="SnapshotVerticalSpreadPosition"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext; }

[MessagePackObject]
public sealed record IronCondorPositionChangedEvent : PositionChangedEvent;
[MessagePackObject]
public sealed record VerticalSpreadPositionChangedEvent : PositionChangedEvent;
