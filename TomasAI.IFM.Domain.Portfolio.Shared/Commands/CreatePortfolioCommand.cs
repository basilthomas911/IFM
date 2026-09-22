using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Commands;

/// <summary>Represents the CreatePortfolioCommand actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record CreatePortfolioCommand : ICommand<PortfolioId>, ICommandRetryIdentity
{
    public const string Actor = "PortfolioCommand";
    public const string Verb = "CreatePortfolio";
    public const int ErrorId = 34000;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public PortfolioId EntityId { get; init; } = default!;
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioBoundedContext;
    [Key(6)] public PortfolioReadModel Portfolio { get; init; } = default!;
    [Key(7)] public Guid IdempotencyKey { get; init; } = default!;
    [Key(8)] public Guid CorrelationId { get; init; }
    [Key(9)] public DateTime RequestedOnUtc { get; init; }
    [Key(10)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public string CommandName => nameof(CreatePortfolioCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Subject.Name;

    /// <summary>Initializes an empty message for serialization.</summary>
    public CreatePortfolioCommand() { }

    /// <summary>Initializes the command from its application values.</summary>
    /// <param name="portfolio">The Portfolio command value.</param>
    /// <param name="idempotencyKey">The IdempotencyKey command value.</param>
    public CreatePortfolioCommand(PortfolioReadModel portfolio, Guid idempotencyKey)
    {
        Portfolio = portfolio;
        IdempotencyKey = idempotencyKey;
    }

    ICommand ICommandRetryIdentity.ForRetryIdentity() => this with { CorrelationId = Guid.Empty, RequestedOnUtc = default };
}
