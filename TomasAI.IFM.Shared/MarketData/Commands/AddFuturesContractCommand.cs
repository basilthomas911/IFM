using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// MessagePack-serializable command to add or update a futures contract.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern:
/// - Base command reserves keys 0..5.
/// - Derived payload keys start at 6.
/// - Includes a parameterless ctor for serializers and a <see cref="SerializationConstructorAttribute"/>-annotated ctor.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddFuturesContractCommand
    : ICommand<FuturesContractId>
{
    public const string Actor = "FuturesContractCommand";
    public const string Verb = "Add";
    public const int ErrorId = 6001;

    // Serialized members (unique keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Default;  
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesContractId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    [Key(6)] public FuturesContractV2ReadModel Contract { get; init; }
    [Key(7)] public bool Overwrite { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Parameterless constructor required by serializers.</summary>
    public AddFuturesContractCommand()
    {
    }

    /// <summary>Create a new AddFuturesContractCommand.</summary>
    /// <param name="contract">Futures contract view model (required).</param>
    /// <param name="overwrite">Whether to overwrite an existing contract.</param>
    public AddFuturesContractCommand(FuturesContractV2ReadModel contract, bool overwrite = false)
    {
        Contract = IsArgumentNull.Set(contract);
        EntityId = contract.Id;
        Overwrite = overwrite;
    }

    /// <summary>
    /// MessagePack serialization constructor. Keys 0..5 belong to the base command; derived keys start at 6.
    /// </summary>
    [SerializationConstructor]
    public AddFuturesContractCommand(
        Guid commandId,                     // Key(0)
        ActorSubject subject,               // Key(1)
        bool postEvents,                    // Key(2)
        FuturesContractId entityId,         // Key(3)
        int errorCode,                      // Key(4)
        BoundedContextName routeTo,         // Key(5)
        FuturesContractV2ReadModel contract,// Key(6)
        bool overwrite)                     // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Contract = contract;
        Overwrite = overwrite;
    }

}