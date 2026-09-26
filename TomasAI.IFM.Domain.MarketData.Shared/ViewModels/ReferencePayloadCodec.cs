using MessagePack;
using MessagePack.Resolvers;
using System.Buffers;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Encodes and validates append-only futures reference payloads independently of their database provider.
/// </summary>
public static class ReferencePayloadCodec
{
    static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithCompression(MessagePackCompression.Lz4BlockArray);

    static readonly MessagePackSerializerOptions ContentOptions = Options.WithCompression(MessagePackCompression.None);

    /// <summary>Encodes a futures-contract reference payload.</summary>
    public static byte[] Write(FuturesContractV3ReadModel value)
    {
        ValidateReview(value.SchemaVersion, value.ReviewState, () => FuturesReferenceQualification.Errors(value));
        return Encode(value);
    }

    /// <summary>Encodes a futures-option-contract reference payload.</summary>
    public static byte[] Write(FuturesOptionContractReadModel value)
    {
        ValidateReview(value.SchemaVersion, value.ReviewState, () => FuturesReferenceQualification.Errors(value));
        if (value.StrikePriceDecimal.HasValue)
            _ = value.GetExactStrikePrice();
        return Encode(value);
    }

    /// <summary>Reads a futures-contract payload and verifies its legacy-column identity.</summary>
    public static FuturesContractV3ReadModel ReadFuture(byte[]? bytes, FuturesContractV3ReadModel legacy)
    {
        if (bytes is null)
            return legacy;
        var value = Decode<FuturesContractV3ReadModel>(bytes);
        if (value.SchemaVersion is < 0 or > 1 || value.ContractId != legacy.ContractId || value.Symbol != legacy.Symbol
            || value.Description != legacy.Description || value.LocalSymbol != legacy.LocalSymbol
            || value.SecurityType != legacy.SecurityType || value.Currency != legacy.Currency
            || value.Exchange != legacy.Exchange || value.Multiplier != legacy.Multiplier
            || value.LastTradeDate != legacy.LastTradeDate || value.OnTheRun != legacy.OnTheRun || value.Rollover != legacy.Rollover)
            throw new InvalidDataException("Reference payload conflicts with futures columns; migration/reconciliation is required.");
        return value;
    }

    /// <summary>Reads a futures-option payload and verifies its legacy-column identity.</summary>
    public static FuturesOptionContractReadModel ReadOption(byte[]? bytes, FuturesOptionContractReadModel legacy)
    {
        if (bytes is null)
            return legacy;
        var value = Decode<FuturesOptionContractReadModel>(bytes);
        if (value.SchemaVersion is < 0 or > 1 || value.ContractId != legacy.ContractId || value.Symbol != legacy.Symbol
            || value.Description != legacy.Description || value.LocalSymbol != legacy.LocalSymbol
            || value.SecurityType != legacy.SecurityType || value.Currency != legacy.Currency
            || value.Exchange != legacy.Exchange || value.Multiplier != legacy.Multiplier
            || value.ContractMonth != legacy.ContractMonth || value.StrikePrice != legacy.StrikePrice || value.OptionType != legacy.OptionType)
            throw new InvalidDataException("Reference payload conflicts with option columns; migration/reconciliation is required.");
        if (value.StrikePriceDecimal.HasValue)
            _ = value.GetExactStrikePrice();
        return value;
    }

    /// <summary>Reads and validates a standalone futures-option payload.</summary>
    public static FuturesOptionContractReadModel ReadOption(byte[] bytes)
    {
        var value = Decode<FuturesOptionContractReadModel>(bytes);
        ValidateReview(value.SchemaVersion, value.ReviewState, () => FuturesReferenceQualification.Errors(value));
        if (value.StrikePriceDecimal.HasValue)
            _ = value.GetExactStrikePrice();
        return value;
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
        var writer = new ArrayBufferWriter<byte>();
        MessagePackSerializer.Serialize(writer, value, ContentOptions);
        if (writer.WrittenCount > 65536)
            throw new InvalidDataException("Reference content exceeds 64 KiB.");
        var bytes = MessagePackSerializer.Serialize(value, Options);
        if (bytes.Length > 65536)
            throw new InvalidDataException("Reference payload exceeds 64 KiB.");
        return bytes;
    }

    static T Decode<T>(byte[] bytes)
    {
        if (bytes.Length is 0 or > 65536)
            throw new InvalidDataException("Reference payload length is invalid.");
        try
        {
            var value = MessagePackSerializer.Deserialize<T>(bytes, Options);
            return value is null
                ? throw new InvalidDataException("Reference payload cannot be nil.")
                : value;
        }
        catch (Exception ex) when (ex is MessagePackSerializationException or EndOfStreamException)
        {
            throw new InvalidDataException(
                "Reference payload is incompatible or truncated; migration/reconciliation is required.", ex);
        }
    }
}
