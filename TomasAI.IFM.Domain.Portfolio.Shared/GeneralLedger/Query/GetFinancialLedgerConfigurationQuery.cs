using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Represents the GetFinancialLedgerConfigurationQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFinancialLedgerConfigurationQuery : IFinancialQueryMessage<GetFinancialLedgerConfigurationRequest, FinancialLedgerConfiguration>
{
    public const string Actor = "PortfolioFinancialQuery";
    public const string Verb = "GetFinancialLedgerConfiguration";
    public const int ErrorId = FinancialReasons.PersistenceFailed;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public LedgerPortfolioId QueryEntityId { get; init; } = new(0);
    [Key(2)] public FinancialReadScope Scope { get; init; } = new();
    [Key(3)] public Guid CorrelationId { get; init; }
    [Key(4)] public DateTime RequestedAtUtc { get; init; }

    [IgnoreMember] public GetFinancialLedgerConfigurationRequest Parameters
    {
        get => new();
        init
        {
            ArgumentNullException.ThrowIfNull(value);
        }
    }
    [IgnoreMember] public int SchemaVersion => 1;
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => QueryEntityId;

    /// <summary>Initializes an empty query for serialization.</summary>
    public GetFinancialLedgerConfigurationQuery() { }

    /// <summary>Initializes the query from its request values.</summary>
    /// <param name="parameters">The query request.</param>
    public GetFinancialLedgerConfigurationQuery(GetFinancialLedgerConfigurationRequest parameters) => Parameters = parameters;
}
