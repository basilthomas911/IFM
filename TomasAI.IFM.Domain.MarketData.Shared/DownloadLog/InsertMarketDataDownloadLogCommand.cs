using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

[MessagePackObject]
public sealed record InsertMarketDataDownloadLogCommand : ICommand<DownloadLogId>
{
    public const string Actor = "DownloadLogCommand";
    public const string Verb = "InsertMarketDataDownloadLog";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = default!;
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public DownloadLogId EntityId { get; init; } = default!;
    [Key(4)] public int ErrorCode { get; init; } = 6050;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.DownloadLogBoundedContext;
    [Key(6)] public MarketDataDownloadOutcome Outcome { get; init; } = default!;
    [Key(7)] public string PayloadSha256 { get; init; } = "";
    [IgnoreMember] public string CommandName => nameof(InsertMarketDataDownloadLogCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => "DownloadLogCommandActor";
    [IgnoreMember] public DateTime OriginatedOn => Outcome.RequestedAtUtc;
    [IgnoreMember] public string OriginatedBy => "MarketDataImport";

    public InsertMarketDataDownloadLogCommand() { }
    public InsertMarketDataDownloadLogCommand(MarketDataDownloadOutcome outcome)
    {
        Outcome = outcome;
        PayloadSha256 = outcome.ComputeHash();
        CommandId = MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId);
        EntityId = new(outcome.ImportCommandId);
        Subject = new(ActorType.Command, Actor, Verb, EntityId.Format());
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Outcome);
        if (CommandId != MarketDataDownloadOutcome.LoggingCommandId(Outcome.ImportCommandId)
            || EntityId?.ImportCommandId != Outcome.ImportCommandId
            || RouteTo != BoundedContextName.DownloadLogBoundedContext
            || Subject.ActorType != ActorType.Command || Subject.Name != Actor
            || Subject.Verb != Verb || Subject.EntityId != EntityId.Format()
            || PayloadSha256 != Outcome.ComputeHash())
            throw new ArgumentException("DownloadLog identity, route or payload hash does not match its outcome.");
    }
}
