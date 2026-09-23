using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Append-only reference payload. Legacy columns remain readable by old readers.
/// A mixed-version write must never attach stale qualification to changed legacy facts.</summary>
public static class ReferencePayloadCodec
{
    public static byte[] Write(FuturesContractV3ReadModel value)
    {
        ValidateReview(value.SchemaVersion, value.ReviewState, () => FuturesReferenceQualification.Errors(value));
        return Encode(value);
    }
    public static byte[] Write(FuturesOptionContractReadModel value)
    {
        ValidateReview(value.SchemaVersion, value.ReviewState, () => FuturesReferenceQualification.Errors(value));
        if (value.StrikePriceDecimal.HasValue) _ = value.GetExactStrikePrice();
        return Encode(value);
    }

    static void ValidateReview(int schema, ReferenceReviewState review, Func<IReadOnlyList<string>> errors)
    {
        if (schema is < 0 or > 1 || !Enum.IsDefined(review)
            || schema == 0 && review != ReferenceReviewState.Unknown)
            throw new InvalidDataException("Reference schema/review state is invalid.");
        if (review == ReferenceReviewState.Reviewed && errors() is { Count: > 0 } invalid)
            throw new InvalidDataException("Reference review is incomplete: " + string.Join(", ", invalid));
    }

    static byte[] Encode<T>(T value)
    {
        if (MessagePackBinarySerializer.MeasureContent(value) > 65536) throw new InvalidDataException("Reference content exceeds 64 KiB.");
        var bytes = MessagePackBinarySerializer.Shared.Serialize(value)!;
        if (bytes.Length > 65536) throw new InvalidDataException("Reference payload exceeds 64 KiB.");
        return bytes;
    }

    public static FuturesContractV3ReadModel ReadFuture(byte[]? bytes, FuturesContractV3ReadModel legacy)
    {
        if (bytes is null) return legacy;
        var value = Decode<FuturesContractV3ReadModel>(bytes);
        if (value.SchemaVersion is < 0 or > 1 || value.ContractId != legacy.ContractId || value.Symbol != legacy.Symbol
            || value.Description != legacy.Description || value.LocalSymbol != legacy.LocalSymbol
            || value.SecurityType != legacy.SecurityType || value.Currency != legacy.Currency
            || value.Exchange != legacy.Exchange || value.Multiplier != legacy.Multiplier
            || value.LastTradeDate != legacy.LastTradeDate || value.OnTheRun != legacy.OnTheRun || value.Rollover != legacy.Rollover)
            throw new InvalidDataException("Reference payload conflicts with futures columns; migration/reconciliation is required.");
        return value;
    }

    public static FuturesOptionContractReadModel ReadOption(byte[]? bytes, FuturesOptionContractReadModel legacy)
    {
        if (bytes is null) return legacy;
        var value = Decode<FuturesOptionContractReadModel>(bytes);
        if (value.SchemaVersion is < 0 or > 1 || value.ContractId != legacy.ContractId || value.Symbol != legacy.Symbol
            || value.Description != legacy.Description || value.LocalSymbol != legacy.LocalSymbol
            || value.SecurityType != legacy.SecurityType || value.Currency != legacy.Currency
            || value.Exchange != legacy.Exchange || value.Multiplier != legacy.Multiplier
            || value.ContractMonth != legacy.ContractMonth || value.StrikePrice != legacy.StrikePrice || value.OptionType != legacy.OptionType)
            throw new InvalidDataException("Reference payload conflicts with option columns; migration/reconciliation is required.");
        if (value.StrikePriceDecimal.HasValue) _ = value.GetExactStrikePrice();
        return value;
    }

    public static FuturesOptionContractReadModel ReadOption(byte[] bytes)
    {
        var value = Decode<FuturesOptionContractReadModel>(bytes);
        ValidateReview(value.SchemaVersion, value.ReviewState, () => FuturesReferenceQualification.Errors(value));
        if (value.StrikePriceDecimal.HasValue) _ = value.GetExactStrikePrice();
        return value;
    }

    static T Decode<T>(byte[] bytes)
    {
        if (bytes.Length is 0 or > 65536) throw new InvalidDataException("Reference payload length is invalid.");
        try
        {
            var value = MessagePackBinarySerializer.Shared.Deserialize<T>(bytes);
            return value is null ? throw new InvalidDataException("Reference payload cannot be nil.") : value;
        }
        catch (Exception ex) when (ex is MessagePackSerializationException or EndOfStreamException)
        { throw new InvalidDataException("Reference payload is incompatible or truncated; migration/reconciliation is required.", ex); }
    }
}
