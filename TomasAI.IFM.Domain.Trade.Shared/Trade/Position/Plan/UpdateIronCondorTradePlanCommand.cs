using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

[MessagePackObject]
public sealed record UpdateIronCondorTradePlanCommand : ICommand<IronCondorTradePlanId>
{
    public const string Actor = "IronCondorTradePlanFunction";
    public const string Verb = "UpdateIronCondorTradePlan";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public IronCondorTradePlanId EntityId { get; init; }
    [Key(4)] public StrategyPositionSnapshot Position { get; init; } = new();
    [Key(5)] public TradePlanParameters Parameters { get; init; } = new();
    [Key(6)] public Guid SourceEventId { get; init; }
    [Key(7)] public DateTime RequestedAtUtc { get; init; }
    [IgnoreMember] public string CommandName => nameof(UpdateIronCondorTradePlanCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionTradePlanBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 27101;
}
