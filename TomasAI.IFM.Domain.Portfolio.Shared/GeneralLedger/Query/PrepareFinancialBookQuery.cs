using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Represents the PrepareFinancialBookQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PrepareFinancialBookQuery : IFinancialQueryMessage<PrepareFinancialBookRequest, FinancialBookSetup>
{
    public const string Actor = "PortfolioFinancialQuery";
    public const string Verb = "PrepareFinancialBook";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId QueryEntityId { get; init; } = new(0);
    [Key(2)] public string? ExecutionAccountReference { get; init; } = default!;
    [Key(3)] public DateOnly PeriodStart { get; init; } = default!;
    [Key(4)] public DateOnly PeriodEnd { get; init; } = default!;
    [Key(5)] public FinancialReadScope Scope { get; init; } = new();
    [Key(6)] public Guid CorrelationId { get; init; }
    [Key(7)] public DateTime RequestedAtUtc { get; init; }

    [IgnoreMember] public PrepareFinancialBookRequest Parameters
    {
        get => new(ExecutionAccountReference, PeriodStart, PeriodEnd);
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            ExecutionAccountReference = value.ExecutionAccountReference;
            PeriodStart = value.PeriodStart;
            PeriodEnd = value.PeriodEnd;
        }
    }
    [IgnoreMember] public int SchemaVersion => 1;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty query for serialization.</summary>
    public PrepareFinancialBookQuery() { }

    /// <summary>Initializes the query from its request values.</summary>
    /// <param name="parameters">The query request.</param>
    public PrepareFinancialBookQuery(PrepareFinancialBookRequest parameters) => Parameters = parameters;
}
