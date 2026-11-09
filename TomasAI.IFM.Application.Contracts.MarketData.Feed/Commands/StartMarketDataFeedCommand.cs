using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;

/// <summary>Requests Start using the API server's current session and prepared subscriptions.</summary>
[MessagePackObject(AllowPrivate = true)]
public record StartMarketDataFeedCommand : ICommand<MarketDataFeedId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "Start";
    public const int ErrorId = 5005;
    public const string CurrentSession = "current";

    public static StartMarketDataFeedCommand ForCurrentSession(Guid commandId) => new()
    {
        CommandId = commandId,
        Subject = new(ActorType.Command, Actor, Verb, CurrentSession)
    };

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    // Former wire slots 3 and 9 are ignored. The API sets these values in memory.
    [IgnoreMember] public MarketDataFeedId EntityId { get; init; } = new();
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.MarketDataFeedBoundedContext;
    // Former payload slots 6..8 are deliberately not reused.
    [IgnoreMember] public TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[]? PreparedContracts { get; init; }

    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822",Justification="Preserve the instance contract consumed by existing callers or BenchmarkDotNet reflection.")]
public DateTime OriginatedOn => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(this)?.OriginatedAtUtc.UtcDateTime ?? default;
    [IgnoreMember] [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822",Justification="Preserve the instance contract consumed by existing callers or BenchmarkDotNet reflection.")]
public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public DateOnly ValueDate => EntityId?.ValueDate ?? default;
}
