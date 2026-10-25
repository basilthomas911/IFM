using System.Buffers;
using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

[MessagePackObject]
public readonly record struct TickDataEntityId(
    [property: Key(0)] string ContractId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] AssetTypeId AssetTypeId) : IActorEntityId
{
    public string Format() => string.Create(
        CultureInfo.InvariantCulture,
        $"{(byte)AssetTypeId}:{ValueDate:yyyyMMdd}:{Uri.EscapeDataString(ContractId)}");

    public static TickDataEntityId Parse(string value) =>
        TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException("TickDataEntityId is malformed.");

    public static bool TryParse(string? value, out TickDataEntityId entityId)
    {
        entityId = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var firstSeparator = value.IndexOf(':', StringComparison.Ordinal);
        var secondSeparator = firstSeparator < 0 ? -1 : value.IndexOf(':', firstSeparator + 1);
        if (firstSeparator <= 0
            || secondSeparator <= firstSeparator + 1
            || !byte.TryParse(
                value.AsSpan(0, firstSeparator),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var assetTypeValue)
            || !Enum.IsDefined((AssetTypeId)assetTypeValue)
            || (AssetTypeId)assetTypeValue == AssetTypeId.Unknown
            || !DateOnly.TryParseExact(
                value.AsSpan(firstSeparator + 1, secondSeparator - firstSeparator - 1),
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var valueDate))
            return false;
        try
        {
            var contractId = Uri.UnescapeDataString(value[(secondSeparator + 1)..]);
            if (string.IsNullOrWhiteSpace(contractId)) return false;
            entityId = new(contractId, valueDate, (AssetTypeId)assetTypeValue);
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}
