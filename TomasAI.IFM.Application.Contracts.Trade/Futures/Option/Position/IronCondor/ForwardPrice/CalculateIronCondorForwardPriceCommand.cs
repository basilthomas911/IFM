using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;

[MessagePackObject]
public sealed record CalculateIronCondorForwardPriceCommand : ICommand<ForwardPriceSessionId>
{
    public const string Actor = "IronCondorForwardPriceFunction";
    public const string Verb = "CalculateIronCondorForwardPrice";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public ForwardPriceSessionId EntityId { get; init; }
    [Key(4)] public StrategyPositionSnapshot Position { get; init; } = new();
    [Key(5)] public IronCondorForwardPriceInputs IronCondorForwardPriceInputs { get; init; } = new();
    [Key(6)] public Guid SourceEventId { get; init; }
    [Key(7)] public DateTime RequestedAtUtc { get; init; }
    /// <summary>Captures all input values for exact duplicate-request validation.</summary>
    public string Fingerprint() => Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(new object[] { EntityId.Format(), Position, IronCondorForwardPriceInputs, SourceEventId, RequestedAtUtc })));
    [IgnoreMember] public string CommandName => nameof(CalculateIronCondorForwardPriceCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionTradePlanBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 27121;
}
