using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Represents the GetFinancialAdmissionSnapshotQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFinancialAdmissionSnapshotQuery : IFinancialQueryMessage<GetFinancialAdmissionSnapshotRequest, FinancialAdmissionSnapshot>
{

    /// <summary>Rehydrates every query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="deploymentKey">The DeploymentKey field.</param>
    /// <param name="underlyingId">The UnderlyingId field.</param>
    /// <param name="scope">The Scope field.</param>
    /// <param name="correlationId">The CorrelationId field.</param>
    /// <param name="requestedAtUtc">The RequestedAtUtc field.</param>
    [SerializationConstructor]
    public GetFinancialAdmissionSnapshotQuery(ActorSubject subject, LedgerPortfolioId entityId, TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogKey deploymentKey, string underlyingId, FinancialReadScope scope, Guid correlationId, DateTime requestedAtUtc)
    {
        Subject = subject;
        EntityId = entityId;
        DeploymentKey = deploymentKey;
        UnderlyingId = underlyingId;
        Scope = scope;
        CorrelationId = correlationId;
        RequestedAtUtc = requestedAtUtc;
    }
    public const string Actor = "CapacityReservationQuery";
    public const string Verb = "GetFinancialAdmissionSnapshot";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId EntityId { get; init; } = new(0);
    [Key(2)] public TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogKey DeploymentKey { get; init; } = default!;
    [Key(3)] public string UnderlyingId { get; init; } = default!;
    [Key(4)] public FinancialReadScope Scope { get; init; } = new();
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public DateTime RequestedAtUtc { get; init; }

    [IgnoreMember] public GetFinancialAdmissionSnapshotRequest Parameters
    {
        get => new(DeploymentKey, UnderlyingId);
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            DeploymentKey = value.DeploymentKey;
            UnderlyingId = value.UnderlyingId;
        }
    }
    [IgnoreMember] public int SchemaVersion => 2;
    [IgnoreMember] public LedgerPortfolioId QueryEntityId => EntityId;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty query for serialization.</summary>
    public GetFinancialAdmissionSnapshotQuery() { }

    /// <summary>Initializes the query from its request values.</summary>
    /// <param name="parameters">The query request.</param>
    public GetFinancialAdmissionSnapshotQuery(GetFinancialAdmissionSnapshotRequest parameters) => Parameters = parameters;
}
