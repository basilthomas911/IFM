using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;
using Newtonsoft.Json;
using MessagePack;

namespace TomasAI.IFM.Shared.OptionPricer.ViewModels;

/// <summary>
/// MessagePack-serializable device configuration for the Option Pricer, including device identity,
/// simulation path settings, batch size, option type, and enablement flag.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys;
/// derived members are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record OptionPricerDeviceReadModel
{
    /// <summary>Unique device identifier.</summary>
    [Key(0)]
    public int DeviceId { get; init; }

    /// <summary>Friendly device name.</summary>
    [Key(1)]
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>Number of paths used for spread simulation.</summary>
    [Key(2)]
    public int SpreadPaths { get; init; }

    /// <summary>Number of paths used for volatility simulation.</summary>
    [Key(3)]
    public int VolatilityPaths { get; init; }

    /// <summary>Maximum batch size the device should process.</summary>
    [Key(4)]
    public int MaxBatchSize { get; init; }

    /// <summary>Option type supported by the device (Put/Call).</summary>
    [Key(5)]
    public OptionType OptionType { get; init; }

    /// <summary>True if the device configuration is enabled.</summary>
    [Key(6)]
    public bool Enabled { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public OptionPricerDeviceReadModel() { }

    /// <summary>
    /// Full constructor to initialize an Option Pricer device configuration.
    /// </summary>
    public OptionPricerDeviceReadModel(
        int deviceId,
        string deviceName,
        int spreadPaths,
        int volatilityPaths,
        int maxBatchSize,
        OptionType optionType,
        bool enabled)
    {
        DeviceId = deviceId;
        DeviceName = deviceName ?? string.Empty;
        SpreadPaths = spreadPaths;
        VolatilityPaths = volatilityPaths;
        MaxBatchSize = maxBatchSize;
        OptionType = optionType;
        Enabled = enabled;
    }

    /// <summary>Derived identifier for the device (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public OptionPricerDeviceEntityId EntityId => new OptionPricerDeviceEntityId(DeviceId, DeviceName);

    /// <summary>
    /// Computes the simulation path count to use:
    /// - 0 when SpreadPaths is 0,
    /// - 2^SpreadPaths when SpreadPaths ≤ 20,
    /// - otherwise returns SpreadPaths.
    /// </summary>
    public int SetPathValue()
    {
        if (SpreadPaths == 0)
            return 0;
        if (SpreadPaths <= 20)
            return 1 << SpreadPaths;
        return SpreadPaths;
    }
}