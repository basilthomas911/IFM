using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Submits one exact consumed order to the durable internal emulator. Never routes to a live broker.</summary>
[MessagePackObject]
public sealed record SubmitEmulatorOrderCommand : ICommand<LedgerPortfolioId>, IFinancialRequest<SubmitEmulatorOrderRequest>
{
    public const string Actor = "EmulatorExecutionCommand";
    public const string Verb = "Submit";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; } = Guid.Empty;
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Command, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; } = true;
    [Key(4)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(5)] public int ErrorCode { get; init; } = 34126;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.CapacityReservationBoundedContext;
    [Key(7)] public Guid OperationId { get; init; } = Guid.Empty;
    [Key(8)] public int PortfolioId { get; init; } = 0;
    [Key(9)] public Guid CorrelationId { get; init; } = Guid.Empty;
    [Key(10)] public Guid CausationId { get; init; } = Guid.Empty;
    [Key(11)] public DateTime RequestedAtUtc { get; init; } = default;
    [Key(12)] public DateTime ExpiresAtUtc { get; init; } = default;
    [Key(13)] public long ExpectedFinancialRevision { get; init; } = 0;
    [Key(14)] public SubmitEmulatorOrderRequest Body { get; init; } = new(new(), Guid.Empty, Guid.Empty, string.Empty);
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(SubmitEmulatorOrderCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}
