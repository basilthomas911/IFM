using System.Security.Cryptography;
using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>
/// Carries one versioned, opaque result produced by a strategy pipeline stage.
/// </summary>
/// <remarks>
/// The workflow validates and stores the payload but does not interpret its stage-specific contents.
/// Payload buffers are defensively copied so they cannot be mutated across actor boundaries.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyStageResultEnvelope
{
    /// <summary>Default maximum serialized payload size for one pipeline stage.</summary>
    public const int DefaultMaximumPayloadBytes = 64 * 1024;

    [IgnoreMember]
    byte[] _payload = [];

    /// <summary>Gets the unique result identifier.</summary>
    [Key(0)]
    public Guid ResultId { get; init; }

    /// <summary>Gets the stable logical name of the result contract.</summary>
    [Key(1)]
    public string ResultType { get; init; } = string.Empty;

    /// <summary>Gets the positive schema version of the result contract.</summary>
    [Key(2)]
    public int SchemaVersion { get; init; }

    /// <summary>Gets the media type used to encode the opaque payload.</summary>
    [Key(3)]
    public string ContentType { get; init; } = "application/x-msgpack";

    /// <summary>Gets a defensive copy of the exact serialized stage payload.</summary>
    [Key(4)]
    public ReadOnlyMemory<byte> Payload
    {
        get => _payload.ToArray();
        init => _payload = value.ToArray();
    }

    /// <summary>Gets the hexadecimal SHA-256 digest of <see cref="Payload"/>.</summary>
    [Key(5)]
    public string PayloadSha256 { get; init; } = string.Empty;

    /// <summary>Gets the UTC market-data timestamp represented by the result.</summary>
    [Key(6)]
    public DateTime MarketDataAsOfUtc { get; init; }

    /// <summary>Gets the UTC timestamp at which the pipeline produced the result.</summary>
    [Key(7)]
    public DateTime ProducedAtUtc { get; init; }

    /// <summary>Creates a valid opaque stage-result envelope and calculates its payload digest.</summary>
    /// <param name="resultId">Unique result identifier.</param>
    /// <param name="resultType">Stable logical result contract name.</param>
    /// <param name="schemaVersion">Positive result schema version.</param>
    /// <param name="payload">Exact serialized result payload.</param>
    /// <param name="marketDataAsOfUtc">UTC market-data timestamp represented by the result.</param>
    /// <param name="producedAtUtc">UTC timestamp at which the pipeline produced the result.</param>
    /// <param name="contentType">Media type used to encode the payload.</param>
    /// <param name="maximumPayloadBytes">Maximum payload size accepted for this stage.</param>
    /// <returns>An immutable opaque result envelope.</returns>
    /// <exception cref="ArgumentException">Thrown when required metadata or the payload is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the schema version or payload limit is invalid, or the payload exceeds the configured limit.
    /// </exception>
    public static StrategyStageResultEnvelope Create(
        Guid resultId,
        string resultType,
        int schemaVersion,
        ReadOnlyMemory<byte> payload,
        DateTime marketDataAsOfUtc,
        DateTime producedAtUtc,
        string contentType = "application/x-msgpack",
        int maximumPayloadBytes = DefaultMaximumPayloadBytes)
    {
        if (resultId == Guid.Empty)
            throw new ArgumentException("A result identifier is required.", nameof(resultId));
        ArgumentException.ThrowIfNullOrWhiteSpace(resultType);
        if (schemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "The schema version must be positive.");
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (maximumPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadBytes), "The maximum payload size must be positive.");
        if (payload.IsEmpty)
            throw new ArgumentException("A stage result payload is required.", nameof(payload));
        if (payload.Length > maximumPayloadBytes)
            throw new ArgumentOutOfRangeException(
                nameof(payload), payload.Length,
                $"The stage result payload exceeds the configured {maximumPayloadBytes}-byte limit.");

        return new StrategyStageResultEnvelope
        {
            ResultId = resultId,
            ResultType = resultType,
            SchemaVersion = schemaVersion,
            ContentType = contentType,
            Payload = payload,
            PayloadSha256 = ComputePayloadSha256(payload.Span),
            MarketDataAsOfUtc = marketDataAsOfUtc,
            ProducedAtUtc = producedAtUtc
        };
    }

    /// <summary>Calculates the canonical hexadecimal SHA-256 digest for serialized payload bytes.</summary>
    /// <param name="payload">Exact serialized payload bytes.</param>
    /// <returns>An uppercase, 64-character hexadecimal digest.</returns>
    public static string ComputePayloadSha256(ReadOnlySpan<byte> payload)
        => Convert.ToHexString(SHA256.HashData(payload));

    /// <summary>Determines whether the stored digest matches the exact stored payload bytes.</summary>
    /// <returns><see langword="true"/> when the digest is a valid SHA-256 match; otherwise <see langword="false"/>.</returns>
    public bool HasValidPayloadSha256()
    {
        if (PayloadSha256.Length != SHA256.HashSizeInBytes * 2)
            return false;

        try
        {
            var expectedHash = Convert.FromHexString(PayloadSha256);
            var actualHash = SHA256.HashData(_payload);
            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Validates the metadata, size, and payload integrity of an opaque stage-result envelope.</summary>
public sealed class StrategyStageResultEnvelopeValidationRules
    : BaseValidationRules, IValidationRules<StrategyStageResultEnvelope>
{
    /// <summary>Error returned when the result identifier is empty.</summary>
    public const string ResultIdErrorMessage = "StrategyStageResultEnvelope: ResultId is required";

    /// <summary>Error returned when the logical result contract name is empty.</summary>
    public const string ResultTypeErrorMessage = "StrategyStageResultEnvelope: ResultType is required";

    /// <summary>Error returned when the result schema version is not positive.</summary>
    public const string SchemaVersionErrorMessage = "StrategyStageResultEnvelope: SchemaVersion must be positive";

    /// <summary>Error returned when the payload media type is empty.</summary>
    public const string ContentTypeErrorMessage = "StrategyStageResultEnvelope: ContentType is required";

    /// <summary>Error returned when the opaque payload is empty.</summary>
    public const string PayloadRequiredErrorMessage = "StrategyStageResultEnvelope: Payload is required";

    /// <summary>Error returned when the opaque payload exceeds its configured stage limit.</summary>
    public const string PayloadLimitErrorMessage = "StrategyStageResultEnvelope: Payload exceeds the configured limit";

    /// <summary>Error returned when the payload digest does not match the stored bytes.</summary>
    public const string PayloadHashErrorMessage = "StrategyStageResultEnvelope: PayloadSha256 does not match Payload";

    readonly Validator _rules;

    /// <summary>Initializes envelope validation with the default 64-KiB payload limit.</summary>
    public StrategyStageResultEnvelopeValidationRules()
        : this(StrategyStageResultEnvelope.DefaultMaximumPayloadBytes) { }

    /// <summary>Initializes envelope validation with a stage-specific payload limit.</summary>
    /// <param name="maximumPayloadBytes">Maximum serialized payload size accepted for the stage.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the payload limit is not positive.</exception>
    public StrategyStageResultEnvelopeValidationRules(int maximumPayloadBytes)
    {
        if (maximumPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadBytes), "The maximum payload size must be positive.");
        _rules = new Validator(maximumPayloadBytes);
    }

    /// <summary>Validates the supplied stage-result envelope.</summary>
    /// <param name="envelope">Envelope to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(StrategyStageResultEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return Validate(envelope, _rules);
    }

    sealed class Validator : AbstractValidator<StrategyStageResultEnvelope>
    {
        public Validator(int maximumPayloadBytes)
        {
            RuleFor(x => x.ResultId).NotEmpty().WithMessage(ResultIdErrorMessage);
            RuleFor(x => x.ResultType).NotEmpty().WithMessage(ResultTypeErrorMessage);
            RuleFor(x => x.SchemaVersion).GreaterThan(0).WithMessage(SchemaVersionErrorMessage);
            RuleFor(x => x.ContentType).NotEmpty().WithMessage(ContentTypeErrorMessage);
            RuleFor(x => x.Payload).Must(static payload => !payload.IsEmpty).WithMessage(PayloadRequiredErrorMessage);
            RuleFor(x => x.Payload)
                .Must(payload => payload.Length <= maximumPayloadBytes)
                .WithMessage(PayloadLimitErrorMessage);
            RuleFor(x => x).Must(static envelope => envelope.HasValidPayloadSha256())
                .WithMessage(PayloadHashErrorMessage);
        }
    }
}
