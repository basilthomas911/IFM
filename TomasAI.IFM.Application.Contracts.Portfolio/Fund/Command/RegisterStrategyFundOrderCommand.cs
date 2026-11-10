using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Commands;

/// <summary>Registers already allocated Portfolio-approved strategy identities for the existing Fund execution lifecycle.</summary>
[MessagePackObject]
public sealed record RegisterStrategyFundOrderCommand : ICommand<PortfolioFundId>, IPortfolioLifecycleCommand
{
    public const string Actor = "PortfolioFundCommand";
    public const string Verb = "RegisterStrategyFundOrder";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public PortfolioFundId EntityId { get; init; }
    [Key(4)] public PortfolioExecutionOrderInstruction Order { get; init; } = new();
    [Key(5)] public Guid WorkflowId { get; init; }
    [Key(6)] public Guid SourceCommandId { get; init; }
    [Key(7)] public long SourceEventVersion { get; init; }
    [Key(8)] public Guid CorrelationId { get; init; }
    [Key(9)] public DateTime RequestedOnUtc { get; init; }
    [Key(10)] public PortfolioAccessContext Access { get; init; } = new();
    [Key(11)] public string UnderlyingContractId { get; init; } = string.Empty;
    [IgnoreMember] public string CommandName => nameof(RegisterStrategyFundOrderCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.PortfolioBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public int ErrorCode => 34289;
    ICommand ICommandRetryIdentity.ForRetryIdentity() => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(this,
        this with { CorrelationId = Guid.Empty, RequestedOnUtc = default });
}
