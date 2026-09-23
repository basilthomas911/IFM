using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Queries;

/// <summary>Requests tradable Databento option definitions for one underlying futures contract and maturity range.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetDatabentoOptionChainRangeQuery : IQuery<OptionContractExpiryReadModel[]>
{
    [IgnoreMember] public const string Actor = "MarketDataQuery";
    [IgnoreMember] public const string Verb = "GetDatabentoOptionChainRange";
    [IgnoreMember] public const int ErrorId = 1063;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = ActorEntityId.Default;
    [Key(2)] public string UnderlyingSymbol { get; set; } = string.Empty;
    [Key(3)] public DateOnly FromMaturityDate { get; set; }
    [Key(4)] public DateOnly ThroughMaturityDate { get; set; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string QueryParams =>
        $"underlyingSymbol={Uri.EscapeDataString(UnderlyingSymbol)}&fromMaturityDate={FromMaturityDate:yyyy-MM-dd}&throughMaturityDate={ThroughMaturityDate:yyyy-MM-dd}";
}
