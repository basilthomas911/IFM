using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
/// <summary>Requests six hours of chart history ending within the supplied trading session.</summary>
[MessagePackObject]
public sealed record GetIronCondorTradePlanChartQuery : IQuery<StrategyTradePlanChartSeries>
{
    [IgnoreMember] public const string Actor = GetCurrentIronCondorTradePlanQuery.Actor;
    [IgnoreMember] public const string Verb = "GetIronCondorTradePlanChart";
    [IgnoreMember] public const int ErrorId = 27128;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public IronCondorTradePlanId PlanId { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}
