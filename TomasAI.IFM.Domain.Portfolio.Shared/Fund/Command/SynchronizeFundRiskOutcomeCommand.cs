using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Commands;

/// <summary>Represents the SynchronizeFundRiskOutcomeCommand actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record SynchronizeFundRiskOutcomeCommand : ICommand<PortfolioFundId>, ICommandRetryIdentity
{

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="expectedVersion">The ExpectedVersion field.</param>
    /// <param name="evidence">The Evidence field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="requestedOnUtc">The RequestedOnUtc field.</param>
    /// <param name="access">The Access field.</param>
    [SerializationConstructor]
    public SynchronizeFundRiskOutcomeCommand(Guid commandId, ActorSubject subject, bool postEvents, PortfolioFundId entityId, int errorCode, BoundedContextName routeTo, long expectedVersion, Financial.RiskTerminalEvidence evidence, Guid correlationId, DateTime requestedOnUtc, PortfolioAccessContext access)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ExpectedVersion = expectedVersion;
        Evidence = evidence;
        CorrelationId = correlationId;
        RequestedOnUtc = requestedOnUtc;
        Access = access;
    }
    public const string Actor = "PortfolioFundCommand";
    public const string Verb = "SynchronizeFundRiskOutcome";
    public const int ErrorId = 34000;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public PortfolioFundId EntityId { get; init; } = default!;
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioBoundedContext;
    [Key(6)] public long ExpectedVersion { get; init; } = default!;
    [Key(7)] public Financial.RiskTerminalEvidence Evidence { get; init; } = default!;
    [Key(8)] public Guid CorrelationId { get; init; }
    [Key(9)] public DateTime RequestedOnUtc { get; init; }
    [Key(10)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public string CommandName => nameof(SynchronizeFundRiskOutcomeCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Subject.Name;

    /// <summary>Initializes an empty message for serialization.</summary>
    public SynchronizeFundRiskOutcomeCommand() { }

    /// <summary>Initializes the command from its application values.</summary>
    /// <param name="expectedVersion">The ExpectedVersion command value.</param>
    /// <param name="evidence">The Evidence command value.</param>
    public SynchronizeFundRiskOutcomeCommand(long expectedVersion, Financial.RiskTerminalEvidence evidence)
    {
        ExpectedVersion = expectedVersion;
        Evidence = evidence;
    }

    ICommand ICommandRetryIdentity.ForRetryIdentity() => this with { CorrelationId = Guid.Empty, RequestedOnUtc = default };
}
