using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

/// <summary>Identifies one rollover-compatible VX front/back event stream.</summary>
[MessagePackObject]
public readonly record struct FuturesVxTermStructureSignalEntityId : IActorEntityId
{
    /// <summary>Gets the applicable futures trading date.</summary>
    [Key(0)] public DateOnly ValueDate { get; init; }
    /// <summary>Gets the front VX contract ID.</summary>
    [Key(1)] public string FrontContractId { get; init; }
    /// <summary>Gets the back VX contract ID.</summary>
    [Key(2)] public string BackContractId { get; init; }
    /// <summary>Gets the calculation configuration ID.</summary>
    [Key(3)] public string ConfigurationId { get; init; }

    /// <summary>Initializes a rollover-compatible VX curve identity.</summary>
    [SerializationConstructor]
    public FuturesVxTermStructureSignalEntityId(
        DateOnly valueDate,
        string frontContractId,
        string backContractId,
        string configurationId)
    {
        ValueDate = valueDate;
        FrontContractId = frontContractId;
        BackContractId = backContractId;
        ConfigurationId = configurationId;
    }

    /// <summary>Formats the actor routing identity.</summary>
    public string Format() => string.Join('|', ValueDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture),
        Uri.EscapeDataString(FrontContractId), Uri.EscapeDataString(BackContractId),
        Uri.EscapeDataString(ConfigurationId));

    /// <summary>Parses a formatted identity.</summary>
    public static FuturesVxTermStructureSignalEntityId Parse(string value) =>
        TryParse(value, out var result) ? result : throw new FormatException("VX term-structure identity is malformed.");

    /// <summary>Attempts to parse a formatted identity.</summary>
    public static bool TryParse(string? value, out FuturesVxTermStructureSignalEntityId result)
    {
        result = default;
        var parts = value?.Split('|');
        if (parts is not { Length: 4 }
            || !DateOnly.TryParseExact(parts[0], "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) return false;
        result = new(date, Uri.UnescapeDataString(parts[1]), Uri.UnescapeDataString(parts[2]),
            Uri.UnescapeDataString(parts[3]));
        return new FuturesVxTermStructureSignalEntityIdValidationRules().Execute(result).Length == 0;
    }

    /// <inheritdoc />
    public override string ToString() => Format();
}
