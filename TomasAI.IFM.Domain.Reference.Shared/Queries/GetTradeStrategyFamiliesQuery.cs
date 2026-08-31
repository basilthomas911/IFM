using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed class GetTradeStrategyFamiliesQuery : IQuery<TradeStrategyFamilyReadModel[]>
{
    [IgnoreMember] public const string Actor = "ReferenceQuery";
    [IgnoreMember] public const string Verb = "GetTradeStrategyFamilies";
    [IgnoreMember] public const int ErrorId = 1061;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = ActorEntityId.Default;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}
