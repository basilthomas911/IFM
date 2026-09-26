using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetDatabentoOptionChainQuery : IQuery<FuturesOptionContractReadModel[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetDatabentoOptionChainQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="underlyingSymbol">The UnderlyingSymbol field.</param>
    /// <param name="providerRoot">The ProviderRoot field.</param>
    /// <param name="maturityDate">The MaturityDate field.</param>
    [SerializationConstructor]
    public GetDatabentoOptionChainQuery(ActorSubject subject, IActorEntityId entityId, string underlyingSymbol, string providerRoot, DateOnly maturityDate)
    {
        Subject = subject;
        EntityId = entityId;
        UnderlyingSymbol = underlyingSymbol;
        ProviderRoot = providerRoot;
        MaturityDate = maturityDate;
    }
    [IgnoreMember] public const string Actor = "MarketDataQuery";
    [IgnoreMember] public const string Verb = "GetDatabentoOptionChain";
    [IgnoreMember] public const int ErrorId = 1064;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = ActorEntityId.Default;
    [Key(2)] public string UnderlyingSymbol { get; set; } = string.Empty;
    [Key(3)] public string ProviderRoot { get; set; } = string.Empty;
    [Key(4)] public DateOnly MaturityDate { get; set; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string QueryParams =>
        $"underlyingSymbol={Uri.EscapeDataString(UnderlyingSymbol)}&providerRoot={Uri.EscapeDataString(ProviderRoot)}&maturityDate={MaturityDate:yyyy-MM-dd}";
}
