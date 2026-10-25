using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Identifies one exact futures-contract trading session.</summary>
[MessagePackObject]
public readonly record struct FuturesVwapSignalEntityId : IActorEntityId
{
    /// <summary>Gets the actual futures contract identity.</summary>
    [Key(0)] public string ContractId { get; init; }
    /// <summary>Gets the futures session value date.</summary>
    [Key(1)] public DateOnly ValueDate { get; init; }
    /// <summary>Gets the calculation configuration identity.</summary>
    [Key(2)] public string ConfigurationId { get; init; }

    /// <summary>Initializes a session VWAP identity.</summary>
    [SerializationConstructor]
    public FuturesVwapSignalEntityId(string contractId, DateOnly valueDate, string configurationId)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        ConfigurationId = configurationId;
    }

    /// <summary>Formats the canonical actor routing identity.</summary>
    public string Format() => string.Join('|', Uri.EscapeDataString(ContractId),
        ValueDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture), Uri.EscapeDataString(ConfigurationId));

    /// <summary>Parses a canonical session VWAP identity.</summary>
    public static FuturesVwapSignalEntityId Parse(string value) => TryParse(value, out var result)
        ? result : throw new FormatException("Futures VWAP identity is malformed.");

    /// <summary>Attempts to parse a canonical session VWAP identity.</summary>
    public static bool TryParse(string? value, out FuturesVwapSignalEntityId result)
    {
        result = default;
        var parts = value?.Split('|');
        if (parts is not { Length: 3 }
            || !DateOnly.TryParseExact(parts[1], "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) return false;
        result = new(Uri.UnescapeDataString(parts[0]), date, Uri.UnescapeDataString(parts[2]));
        return new FuturesVwapSignalEntityIdValidationRules().Execute(result).Length == 0;
    }

    /// <inheritdoc />
    public override string ToString() => Format();
}
