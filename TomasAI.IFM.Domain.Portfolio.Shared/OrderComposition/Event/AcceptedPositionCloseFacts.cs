using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;


/// <summary>Exact accepted close claim; reconstruction never infers a target position from current orders.</summary>
[MessagePackObject]
public sealed record AcceptedPositionCloseFacts(
    [property:Key(0)] string ActorName,
    [property:Key(1)] string TargetPositionId,
    [property:Key(2)] int TargetOrderId,
    [property:Key(3)] int TargetTradeId,
    [property:Key(4)] int TargetFundId);
