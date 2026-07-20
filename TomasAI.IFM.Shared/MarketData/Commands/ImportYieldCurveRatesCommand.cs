using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// Command to import a batch of yield curve rates for a given import date.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.YieldCurveRateBoundedContext"/> with error code 6010.
/// The entity identifier groups the import by year.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ImportYieldCurveRatesCommand : ICommand<YieldCurveRateEntityId>
{
    public const string Actor = "YieldCurveRateCommand";
    public const string Verb = "Import";
    public const int ErrorId = 6010;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public YieldCurveRateEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Date associated with the imported yield curve rates.</summary>
    [Key(6)]
    public DateTime ImportDate { get; init; }

    /// <summary>Collection of yield curve rate view models to import.</summary>
    [Key(7)]
    public YieldCurveRateReadModel[] YieldCurveRates { get; init; } = [];

    /// <summary>Parameterless constructor required for MessagePack deserialization.</summary>
    public ImportYieldCurveRatesCommand() { }

    /// <summary>
    /// Creates a new command to import yield curve rates.
    /// </summary>
    /// <param name="importDate">The import date (used to derive the entity year).</param>
    /// <param name="yieldCurveRates">Array of yield curve rates (cannot be null).</param>
    public ImportYieldCurveRatesCommand(DateTime importDate, YieldCurveRateReadModel[] yieldCurveRates)
    {
        ImportDate = importDate;
        YieldCurveRates = yieldCurveRates ?? throw new ArgumentNullException(nameof(yieldCurveRates));
        EntityId = new YieldCurveRateEntityId(importDate.Year);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.YieldCurveRateBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public ImportYieldCurveRatesCommand(
        Guid commandId,                       // Key(0)
        ActorSubject subject,                  // Key(1)
        bool postEvents,                       // Key(2)
        YieldCurveRateEntityId entityId,        // Key(3)
        int errorCode,                         // Key(4)
        BoundedContextName routeTo,            // Key(5)
        DateTime importDate,                   // Key(6)
        YieldCurveRateReadModel[] yieldCurveRates) // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ImportDate = importDate;
        YieldCurveRates = yieldCurveRates;
    }
}