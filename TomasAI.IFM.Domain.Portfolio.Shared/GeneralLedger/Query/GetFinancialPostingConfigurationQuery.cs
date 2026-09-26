using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Represents the GetFinancialPostingConfigurationQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFinancialPostingConfigurationQuery : IFinancialQueryMessage<GetFinancialPostingConfigurationRequest, FinancialPostingConfiguration>
{

    /// <summary>Rehydrates every query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="accountingDate">The AccountingDate field.</param>
    /// <param name="scope">The Scope field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="requestedAtUtc">The RequestedAtUtc field.</param>
    [SerializationConstructor]
    public GetFinancialPostingConfigurationQuery(ActorSubject subject, LedgerPortfolioId entityId, DateOnly accountingDate, FinancialReadScope scope, Guid correlationId, DateTime requestedAtUtc)
    {
        Subject = subject;
        EntityId = entityId;
        AccountingDate = accountingDate;
        Scope = scope;
        CorrelationId = correlationId;
        RequestedAtUtc = requestedAtUtc;
    }
    public const string Actor = "GeneralLedgerQuery";
    public const string Verb = "GetFinancialPostingConfiguration";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(2)] public DateOnly AccountingDate { get; init; } = default!;
    [Key(3)] public FinancialReadScope Scope { get; init; } = new();
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public DateTime RequestedAtUtc { get; init; }

    [IgnoreMember] public GetFinancialPostingConfigurationRequest Parameters
    {
        get => new(AccountingDate);
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            AccountingDate = value.AccountingDate;
        }
    }
    [IgnoreMember] public int SchemaVersion => 2;
    [IgnoreMember] public LedgerPortfolioId QueryEntityId => EntityId;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty query for serialization.</summary>
    public GetFinancialPostingConfigurationQuery() { }

    /// <summary>Initializes the query from its request values.</summary>
    /// <param name="parameters">The query request.</param>
    public GetFinancialPostingConfigurationQuery(GetFinancialPostingConfigurationRequest parameters) => Parameters = parameters;
}
