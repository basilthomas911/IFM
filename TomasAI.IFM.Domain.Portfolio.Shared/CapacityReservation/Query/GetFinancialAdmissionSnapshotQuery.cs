using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Represents the GetFinancialAdmissionSnapshotQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFinancialAdmissionSnapshotQuery : IFinancialQueryMessage<GetFinancialAdmissionSnapshotRequest, FinancialAdmissionSnapshot>
{
    public const string Actor = "PortfolioFinancialQuery";
    public const string Verb = "GetFinancialAdmissionSnapshot";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId QueryEntityId { get; init; } = new(0);
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
    [IgnoreMember] public int SchemaVersion => 1;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty query for serialization.</summary>
    public GetFinancialAdmissionSnapshotQuery() { }

    /// <summary>Initializes the query from its request values.</summary>
    /// <param name="parameters">The query request.</param>
    public GetFinancialAdmissionSnapshotQuery(GetFinancialAdmissionSnapshotRequest parameters) => Parameters = parameters;
}
