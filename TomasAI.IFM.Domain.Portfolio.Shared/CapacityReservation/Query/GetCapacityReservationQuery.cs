using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Represents the GetCapacityReservationQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetCapacityReservationQuery : IFinancialQueryMessage<GetCapacityReservationRequest, FinancialReservationView>
{
    public const string Actor = "PortfolioFinancialQuery";
    public const string Verb = "GetCapacityReservation";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId QueryEntityId { get; init; } = new(0);
    [Key(2)] public Guid ReservationId { get; init; } = default!;
    [Key(3)] public FinancialReadScope Scope { get; init; } = new();
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public DateTime RequestedAtUtc { get; init; }

    [IgnoreMember] public GetCapacityReservationRequest Parameters
    {
        get => new(ReservationId);
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            ReservationId = value.ReservationId;
        }
    }
    [IgnoreMember] public int SchemaVersion => 1;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty query for serialization.</summary>
    public GetCapacityReservationQuery() { }

    /// <summary>Initializes the query from its request values.</summary>
    /// <param name="parameters">The query request.</param>
    public GetCapacityReservationQuery(GetCapacityReservationRequest parameters) => Parameters = parameters;
}
