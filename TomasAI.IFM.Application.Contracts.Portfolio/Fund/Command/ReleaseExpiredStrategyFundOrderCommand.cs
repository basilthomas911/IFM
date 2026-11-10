using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Commands;

/// <summary>Releases one unsubmitted strategy setup after its Trade expiry and financial release are committed.</summary>
[MessagePackObject]
public sealed record ReleaseExpiredStrategyFundOrderCommand : ICommand<PortfolioFundId>, IPortfolioLifecycleCommand
{
    public const string Actor = "PortfolioFundCommand";
    public const string Verb = "ReleaseExpiredStrategyFundOrder";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public PortfolioFundId EntityId { get; init; }
    [Key(4)] public int OrderId { get; init; }
    [Key(5)] public long ExpectedVersion { get; init; }
    [Key(6)] public Guid SourceCommandId { get; init; }
    [Key(7)] public Guid FinancialOperationId { get; init; }
    [Key(8)] public Guid CorrelationId { get; init; }
    [Key(9)] public DateTime RequestedOnUtc { get; init; }
    [Key(10)] public PortfolioAccessContext Access { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(ReleaseExpiredStrategyFundOrderCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.PortfolioBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public int ErrorCode => 34290;
    ICommand ICommandRetryIdentity.ForRetryIdentity() => MessageOrigins.Copy(this, this with { CorrelationId = Guid.Empty, RequestedOnUtc = default });
}
