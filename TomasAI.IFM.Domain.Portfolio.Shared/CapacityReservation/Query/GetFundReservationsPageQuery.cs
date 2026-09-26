using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Represents the GetFundReservationsPageQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundReservationsPageQuery : IFinancialQueryMessage<GetFundReservationsPageRequest, FinancialPage<FinancialReservationView>>
{

    /// <summary>Rehydrates every query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    /// <param name="cursor">The Cursor field.</param>
    /// <param name="scope">The Scope field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="requestedAtUtc">The RequestedAtUtc field.</param>
    [SerializationConstructor]
    public GetFundReservationsPageQuery(ActorSubject subject, LedgerPortfolioId entityId, int pageSize, FinancialPageCursor? cursor, FinancialReadScope scope, Guid correlationId, DateTime requestedAtUtc)
    {
        Subject = subject;
        EntityId = entityId;
        PageSize = pageSize;
        Cursor = cursor;
        Scope = scope;
        CorrelationId = correlationId;
        RequestedAtUtc = requestedAtUtc;
    }
    public const string Actor = "CapacityReservationQuery";
    public const string Verb = "GetFundReservationsPage";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(2)] public int PageSize { get; init; } = default!;
    [Key(3)] public FinancialPageCursor? Cursor { get; init; } = default!;
    [Key(4)] public FinancialReadScope Scope { get; init; } = new();
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public DateTime RequestedAtUtc { get; init; }

    [IgnoreMember] public GetFundReservationsPageRequest Parameters
    {
        get => new(PageSize, Cursor);
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            PageSize = value.PageSize;
            Cursor = value.Cursor;
        }
    }
    [IgnoreMember] public int SchemaVersion => 2;
    [IgnoreMember] public LedgerPortfolioId QueryEntityId => EntityId;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty query for serialization.</summary>
    public GetFundReservationsPageQuery() { }

    /// <summary>Initializes the query from its request values.</summary>
    /// <param name="parameters">The query request.</param>
    public GetFundReservationsPageQuery(GetFundReservationsPageRequest parameters) => Parameters = parameters;
}
