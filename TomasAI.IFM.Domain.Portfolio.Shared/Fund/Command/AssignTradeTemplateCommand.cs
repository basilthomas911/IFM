using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Commands;

/// <summary>Represents the AssignTradeTemplateCommand actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record AssignTradeTemplateCommand : ICommand<PortfolioFundId>, ICommandRetryIdentity
{
    public const string Actor = "PortfolioFundCommand";
    public const string Verb = "AssignTradeTemplate";
    public const int ErrorId = 34000;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public PortfolioFundId EntityId { get; init; } = default!;
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioBoundedContext;
    [Key(6)] public FundTradeTemplateAssignmentReadModel Assignment { get; init; } = default!;
    [Key(7)] public long ExpectedVersion { get; init; } = default!;
    [Key(8)] public Guid CorrelationId { get; init; }
    [Key(9)] public DateTime RequestedOnUtc { get; init; }
    [Key(10)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public string CommandName => nameof(AssignTradeTemplateCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Subject.Name;

    /// <summary>Initializes an empty message for serialization.</summary>
    public AssignTradeTemplateCommand() { }

    /// <summary>Initializes the command from its application values.</summary>
    /// <param name="assignment">The Assignment command value.</param>
    /// <param name="expectedVersion">The ExpectedVersion command value.</param>
    public AssignTradeTemplateCommand(FundTradeTemplateAssignmentReadModel assignment, long expectedVersion)
    {
        Assignment = assignment;
        ExpectedVersion = expectedVersion;
    }

    ICommand ICommandRetryIdentity.ForRetryIdentity() => this with { CorrelationId = Guid.Empty, RequestedOnUtc = default };
}
