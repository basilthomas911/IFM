using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Commands;

/// <summary>Represents the CloseManualFundOrderCommand actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record CloseManualFundOrderCommand : ICommand<PortfolioFundId>, ICommandRetryIdentity
{
    public const string Actor = "PortfolioFundCommand";
    public const string Verb = "CloseManualFundOrder";
    public const int ErrorId = 34000;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public PortfolioFundId EntityId { get; init; } = default!;
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioBoundedContext;
    [Key(6)] public ManualFundOrderMutationRequest Request { get; init; } = default!;
    [Key(7)] public Guid CorrelationId { get; init; }
    [Key(8)] public DateTime RequestedOnUtc { get; init; }
    [Key(9)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public string CommandName => nameof(CloseManualFundOrderCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Subject.Name;

    /// <summary>Initializes an empty message for serialization.</summary>
    public CloseManualFundOrderCommand() { }

    /// <summary>Initializes the command from its application values.</summary>
    /// <param name="request">The Request command value.</param>
    public CloseManualFundOrderCommand(ManualFundOrderMutationRequest request)
    {
        Request = request;
    }

    ICommand ICommandRetryIdentity.ForRetryIdentity() => this with { CorrelationId = Guid.Empty, RequestedOnUtc = default };
}
