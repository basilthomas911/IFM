using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
public sealed record EvaluatePortfolioOrderCompositionCommand : ICommand<FinancialExecutionId>, IFinancialRequest<PortfolioOrderCandidate>
{
    public const string Actor = "PortfolioOrderCompositionFunction";
    public const string Verb = "Evaluate";
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public ActorSubject Subject { get; init; } = new(ActorType.Function, Actor, Verb, string.Empty);
    [Key(3)] public bool PostEvents { get; init; }
    [Key(4)] public FinancialExecutionId EntityId { get; init; } = new(0, Guid.Empty);
    [Key(5)] public int ErrorCode { get; init; } = 34130;
    [Key(6)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioOrderCompositionBoundedContext;
    [Key(7)] public Guid OperationId { get; init; }
    [Key(8)] public int PortfolioId { get; init; }
    [Key(9)] public Guid CorrelationId { get; init; }
    [Key(10)] public Guid CausationId { get; init; }
    [Key(11)] public DateTime RequestedAtUtc { get; init; }
    [Key(12)] public DateTime ExpiresAtUtc { get; init; }
    [Key(13)] public long ExpectedFinancialRevision { get; init; }
    [Key(14)] public PortfolioOrderCandidate Body { get; init; } = new();
    [Key(15)] public string InputSha256 { get; init; } = string.Empty;
    [Key(16)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
    [IgnoreMember] public string CommandName => nameof(EvaluatePortfolioOrderCompositionCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
}
