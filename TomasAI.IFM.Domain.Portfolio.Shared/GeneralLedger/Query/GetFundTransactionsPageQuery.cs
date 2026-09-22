using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Represents the GetFundTransactionsPageQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundTransactionsPageQuery : IFinancialQueryMessage<GetFundTransactionsPageRequest, FinancialPage<FinancialTransactionRow>>
{
    public const string Actor = "PortfolioFinancialQuery";
    public const string Verb = "GetFundTransactionsPage";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId QueryEntityId { get; init; } = new(0);
    [Key(2)] public int PageSize { get; init; } = default!;
    [Key(3)] public FinancialPageCursor? Cursor { get; init; } = default!;
    [Key(4)] public FinancialReadScope Scope { get; init; } = new();
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public DateTime RequestedAtUtc { get; init; }

    [IgnoreMember] public GetFundTransactionsPageRequest Parameters
    {
        get => new(PageSize, Cursor);
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            PageSize = value.PageSize;
            Cursor = value.Cursor;
        }
    }
    [IgnoreMember] public int SchemaVersion => 1;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty query for serialization.</summary>
    public GetFundTransactionsPageQuery() { }

    /// <summary>Initializes the query from its request values.</summary>
    /// <param name="parameters">The query request.</param>
    public GetFundTransactionsPageQuery(GetFundTransactionsPageRequest parameters) => Parameters = parameters;
}
