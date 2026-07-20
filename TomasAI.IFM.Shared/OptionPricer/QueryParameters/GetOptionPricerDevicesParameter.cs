using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.OptionPricer.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve option pricer devices.
/// </summary>
[MessagePackObject(false)]
public record GetOptionPricerDevicesParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    [SerializationConstructor]
    public GetOptionPricerDevicesParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => ActorEntityId.Default.Format();
}
