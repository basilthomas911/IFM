using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Commands;

/// <summary>Yield-curve import with contiguous actor-command wire keys.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ImportYieldCurveRatesCommand : ICommand<YieldCurveRateEntityId>
{
    /// <summary>The owning command actor.</summary>
    [IgnoreMember] public const string Actor = "YieldCurveRateCommand";
    /// <summary>The distinct versioned verb.</summary>
    [IgnoreMember] public const string Verb = "Import";
    /// <summary>The stable error code.</summary>
    [IgnoreMember] public const int ErrorId = 6010;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public YieldCurveRateEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [Key(6)] public DateTime ImportDate { get; init; }
    [Key(7)] public ImportDuplicatePolicy DuplicatePolicy { get; init; } = ImportDuplicatePolicy.Overwrite;

    [IgnoreMember] public string CommandName => nameof(ImportYieldCurveRatesCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Creates an empty command for serialization.</summary>
    public ImportYieldCurveRatesCommand() { }

    /// <summary>Creates a yield-curve import with the established application defaults.</summary>
    /// <param name="importDate">The import date.</param>
    /// <param name="duplicatePolicy">The duplicate handling policy.</param>
    public ImportYieldCurveRatesCommand(DateTime importDate,
        ImportDuplicatePolicy duplicatePolicy = ImportDuplicatePolicy.Overwrite)
    {
        ImportDate = importDate;
        DuplicatePolicy = duplicatePolicy;
        EntityId = new YieldCurveRateEntityId(importDate.Year);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.YieldCurveRateBoundedContext;
    }

    /// <summary>Rehydrates the contiguous wire fields in permanent numeric-key order.</summary>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="subject">The actor subject.</param>
    /// <param name="postEvents">Whether to publish domain events.</param>
    /// <param name="entityId">The yield-curve import identity.</param>
    /// <param name="errorCode">The command error code.</param>
    /// <param name="routeTo">The bounded context route.</param>
    /// <param name="importDate">The import date.</param>
    /// <param name="duplicatePolicy">The duplicate policy.</param>
    [SerializationConstructor]
    public ImportYieldCurveRatesCommand(Guid commandId, ActorSubject subject, bool postEvents,
        YieldCurveRateEntityId entityId, int errorCode, BoundedContextName routeTo, DateTime importDate,
        ImportDuplicatePolicy duplicatePolicy)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ImportDate = importDate;
        DuplicatePolicy = duplicatePolicy;
    }
}
