using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Represents the PrepareFinancialBookQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PrepareFinancialBookQuery : IFinancialQueryMessage<PrepareFinancialBookRequest, FinancialBookSetup>
{

    /// <summary>Rehydrates every query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="executionAccountReference">The ExecutionAccountReference field.</param>
    /// <param name="periodStart">The PeriodStart field.</param>
    /// <param name="periodEnd">The PeriodEnd field.</param>
    /// <param name="scope">The Scope field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="requestedAtUtc">The RequestedAtUtc field.</param>
    [SerializationConstructor]
    public PrepareFinancialBookQuery(ActorSubject subject, LedgerPortfolioId entityId, string? executionAccountReference, DateOnly periodStart, DateOnly periodEnd, FinancialReadScope scope, Guid correlationId, DateTime requestedAtUtc)
    {
        Subject = subject;
        EntityId = entityId;
        ExecutionAccountReference = executionAccountReference;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Scope = scope;
        CorrelationId = correlationId;
        RequestedAtUtc = requestedAtUtc;
    }
    public const string Actor = "GeneralLedgerQuery";
    public const string Verb = "PrepareFinancialBook";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId EntityId { get; init; } = new(0);
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
    [IgnoreMember] public int SchemaVersion => 2;
    [IgnoreMember] public LedgerPortfolioId QueryEntityId => EntityId;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty query for serialization.</summary>
    public PrepareFinancialBookQuery() { }

    /// <summary>Initializes the query from its request values.</summary>
    /// <param name="parameters">The query request.</param>
    public PrepareFinancialBookQuery(PrepareFinancialBookRequest parameters) => Parameters = parameters;
}
