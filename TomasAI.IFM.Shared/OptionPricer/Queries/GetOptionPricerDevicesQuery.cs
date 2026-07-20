using MessagePack;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.OptionPricer.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve option pricer devices.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetOptionPricerDevicesQuery : IQuery<OptionPricerDevicesReadModel>
{
    [IgnoreMember] public const string Actor = "OptionPricerDevicesQuery";
    [IgnoreMember] public const string Verb = "GetOptionPricerDevices";
    [IgnoreMember] public const int ErrorId = 1017;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetOptionPricerDevicesQuery()
    {
        EntityId = new GetOptionPricerDevicesParameter();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetOptionPricerDevicesQuery(ActorSubject subject, IActorEntityId entityId)
    {
        Subject = subject;
        EntityId = new GetOptionPricerDevicesParameter();
        ErrorCode = ErrorId;
    }
}

