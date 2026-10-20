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
    /// <summary>Gets captured legacy inputs; omitted inputs are explicitly reported unavailable.</summary>
    [Key(8)] public IronCondorTradePlanInputs? IronCondorTradePlanInputs { get; init; }
    /// <summary>Exact returned ForwardPrice calculation; never a precomputed loss.</summary>
    [Key(9)] public TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice.IronCondorForwardPriceCalculatedEvent? IronCondorForwardPriceCalculated { get; init; }
    /// <summary>Explicit unavailable reason from the ForwardPrice stage.</summary>
    [Key(10)] public string? ForwardPriceFailure { get; init; }
    /// <summary>Identifies the new production path; old stored requests preserve legacy interpretation.</summary>
    [Key(11)] public bool UseForwardPriceFunction { get; init; }
    [IgnoreMember] public string CommandName => nameof(UpdateIronCondorTradePlanCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.StrategyPositionTradePlanBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public int ErrorCode => 27101;
}
