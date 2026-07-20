using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.OptionPricer;

/// <summary>
/// MessagePack-serializable identifier for an Option Pricer device, composed of DeviceId and DeviceName.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation: "DeviceId.DeviceName".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record OptionPricerDeviceEntityId(
    /// <summary>The unique identifier of the device.</summary>
    [property: Key(0)] int DeviceId,
    /// <summary>The friendly name of the device.</summary>
    [property: Key(1)] string DeviceName) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public OptionPricerDeviceEntityId() : this(0, string.Empty) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "DeviceId.DeviceName".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{DeviceId}.{DeviceName}");
}
