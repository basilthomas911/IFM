using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using System.Security.Cryptography;
using FluentValidation;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>
/// Carries a versioned stage result: typed Regime Discovery or Market Condition content, or a legacy opaque payload.
/// </summary>
/// <remarks>
/// The workflow validates and stores versioned result content without interpreting stage-specific decisions.
/// Opaque buffers and typed result collections are defensively copied across actor boundaries.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyStageResultEnvelope
{
    /// <summary>Default maximum opaque payload size or canonical typed-content size for one pipeline stage.</summary>
    public const int DefaultMaximumPayloadBytes = 64 * 1024;

    [IgnoreMember]
    [JsonProperty(nameof(Payload))]
    byte[] _payload = [];
    [IgnoreMember, JsonIgnore] RegimeDiscoveryResult? _regimeResult;
    [IgnoreMember, JsonIgnore] (string Hash, int Size) _regimeFingerprint;

    /// <summary>Identifies the typed Regime Discovery content and its v1 field-fingerprint format.</summary>
    public const string TypedRegimeContentType = "application/vnd.ifm.regime-result.v1";

    /// <summary>Gets a typed Regime Discovery result; new Regime events leave the legacy byte payload empty.</summary>
    /// <remarks>Key 8 is appended so persisted envelopes with keys 0 through 7 remain readable.</remarks>
    [Key(8)]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RegimeDiscoveryResult? RegimeResult
    {
        get => RegimeDiscoveryResultContent.Clone(_regimeResult);
        init
        {
            _regimeResult = RegimeDiscoveryResultContent.Clone(value);
            _regimeFingerprint = _regimeResult is null ? default : RegimeDiscoveryResultContent.Fingerprint(_regimeResult);
        }
    }

    [IgnoreMember, JsonIgnore] MarketConditionAssessmentResult? _assessmentResult;
    [IgnoreMember, JsonIgnore] (string Hash, int Size) _assessmentFingerprint;
    public const string TypedAssessmentContentType = "application/vnd.ifm.market-assessment.v1";

    /// <summary>Typed Market Condition content. New completions leave the legacy byte payload empty.</summary>
    [Key(9)]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public MarketConditionAssessmentResult? AssessmentResult
    {
        get => _assessmentResult?.CopyContent();
        init
        {
            _assessmentResult = value?.CopyContent();
            _assessmentFingerprint = _assessmentResult?.ContentFingerprint() ?? default;
        }
    }

    /// <summary>Creates a typed assessment envelope; transport and storage serialize the outer event.</summary>
    public static StrategyStageResultEnvelope CreateAssessment(MarketConditionAssessmentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.ResultId == Guid.Empty || result.SchemaVersion <= 0)
            throw new ArgumentException("A versioned assessment result identity is required.", nameof(result));
        var envelope = new StrategyStageResultEnvelope
        {
            ResultId = result.ResultId, ResultType = nameof(MarketConditionAssessmentResult), SchemaVersion = result.SchemaVersion,
            ContentType = TypedAssessmentContentType, AssessmentResult = result,
            MarketDataAsOfUtc = result.EvaluatedAtUtc, ProducedAtUtc = result.EvaluatedAtUtc
        };
        if (envelope.ContentSize > DefaultMaximumPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(result), "Assessment content exceeds the configured stage limit.");
        return envelope with { PayloadSha256 = envelope._assessmentFingerprint.Hash };
    }

    /// <summary>Reads typed assessment content, decoding bytes only for legacy persisted envelopes.</summary>
    public MarketConditionAssessmentResult ReadAssessmentResult()
    {
        if (ResultType != nameof(MarketConditionAssessmentResult) || !HasValidPayloadSha256())
            throw new ArgumentException("Invalid assessment result envelope.");
        if (_assessmentResult is not null) return AssessmentResult!;
        if (ContentType != "application/x-msgpack") throw new ArgumentException("Unsupported legacy assessment encoding.");
        return MessagePackSerializer.Deserialize<MarketConditionAssessmentResult>(_payload);
    }

    /// <summary>Gets the canonical typed-content size or legacy encoded payload size used by the stage budget.</summary>
    [IgnoreMember, JsonIgnore, System.Text.Json.Serialization.JsonIgnore] public int ContentSize => _assessmentResult is not null ? _assessmentFingerprint.Size : _regimeResult is null ? _payload.Length : _regimeFingerprint.Size;
    /// <summary>Gets whether either supported result representation is populated.</summary>
    [IgnoreMember, JsonIgnore, System.Text.Json.Serialization.JsonIgnore] public bool HasContent => _assessmentResult is not null || _regimeResult is not null || _payload.Length != 0;

    /// <summary>Creates a typed Regime envelope without serializing an inner message payload.</summary>
    /// <param name="result">The result whose typed fields, metadata and fingerprint are carried by the outer message.</param>
    /// <returns>A defensively copied typed envelope with an empty legacy payload.</returns>
    public static StrategyStageResultEnvelope CreateRegime(RegimeDiscoveryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.ResultId == Guid.Empty || result.SchemaVersion == 0)
            throw new ArgumentException("A versioned Regime result identity is required.", nameof(result));
        var envelope = new StrategyStageResultEnvelope
        {
            ResultId = result.ResultId, ResultType = nameof(RegimeDiscoveryResult), SchemaVersion = result.SchemaVersion,
            ContentType = TypedRegimeContentType, RegimeResult = result,
            MarketDataAsOfUtc = result.MarketDataAsOfUtc, ProducedAtUtc = result.ProducedAtUtc
        };
        if (envelope.ContentSize > DefaultMaximumPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(result), "Regime content exceeds the configured stage limit.");
        return envelope with { PayloadSha256 = envelope._regimeFingerprint.Hash };
    }

    /// <summary>Reads typed Regime content, decoding bytes only for persisted legacy envelopes.</summary>
    /// <returns>The typed result after envelope integrity checks.</returns>
    public RegimeDiscoveryResult ReadRegimeResult()
    {
        if (ResultType != nameof(RegimeDiscoveryResult) || !HasValidPayloadSha256())
            throw new ArgumentException("Invalid Regime Discovery result envelope.");
        if (_regimeResult is not null) return RegimeResult!;
        if (ContentType != "application/x-msgpack")
            throw new ArgumentException("Unsupported legacy Regime result encoding.");
        return MessagePackSerializer.Deserialize<RegimeDiscoveryResult>(_payload);
    }

    /// <summary>Compares validated content and its envelope metadata without serializing either envelope.</summary>
    /// <param name="other">The other accepted result envelope.</param>
    /// <returns>True when both envelopes identify the same complete content.</returns>
    public bool HasSameContent(StrategyStageResultEnvelope? other) => other is not null &&
        HasValidPayloadSha256() && other.HasValidPayloadSha256() && ResultId == other.ResultId &&
        ResultType == other.ResultType && SchemaVersion == other.SchemaVersion && ContentType == other.ContentType &&
        ProducedAtUtc == other.ProducedAtUtc && MarketDataAsOfUtc == other.MarketDataAsOfUtc &&
        string.Equals(PayloadSha256, other.PayloadSha256, StringComparison.OrdinalIgnoreCase);


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
    [JsonIgnore]
    public ReadOnlyMemory<byte> Payload
    {
        get => _payload.ToArray();
        init => _payload = value.ToArray();
    }

    /// <summary>Gets the SHA-256 field fingerprint for typed Regime content, or the byte digest for an opaque payload.</summary>
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

    /// <summary>Checks the typed field fingerprint and metadata, or the legacy payload byte digest.</summary>
    /// <returns><see langword="true"/> when the digest is a valid SHA-256 match; otherwise <see langword="false"/>.</returns>
    public bool HasValidPayloadSha256()
    {
        if (PayloadSha256 is not { Length: SHA256.HashSizeInBytes * 2 })
            return false;
        if (_assessmentResult is not null)
        {
            if (_regimeResult is not null || _payload.Length != 0 || ContentType != TypedAssessmentContentType ||
                ResultType != nameof(MarketConditionAssessmentResult) || ResultId != _assessmentResult.ResultId ||
                SchemaVersion != _assessmentResult.SchemaVersion || ProducedAtUtc != _assessmentResult.EvaluatedAtUtc ||
                MarketDataAsOfUtc != _assessmentResult.EvaluatedAtUtc) return false;
            return string.Equals(PayloadSha256, _assessmentFingerprint.Hash, StringComparison.OrdinalIgnoreCase);
        }
        if (_regimeResult is not null)
        {
            if (_payload.Length != 0 || ContentType != TypedRegimeContentType || ResultType != nameof(RegimeDiscoveryResult) ||
                ResultId != _regimeResult.ResultId || SchemaVersion != _regimeResult.SchemaVersion ||
                ProducedAtUtc != _regimeResult.ProducedAtUtc || MarketDataAsOfUtc != _regimeResult.MarketDataAsOfUtc)
                return false;
            return string.Equals(PayloadSha256, _regimeFingerprint.Hash, StringComparison.OrdinalIgnoreCase);
        }
        if (ContentType is TypedRegimeContentType or TypedAssessmentContentType) return false;

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

/// <summary>Validates the metadata, size, and integrity of typed or opaque stage-result content.</summary>
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

    StrategyStageResultEnvelopeValidationRules(int maximumPayloadBytes)
    {
        if (maximumPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadBytes), "The maximum payload size must be positive.");
        _rules = new Validator(maximumPayloadBytes);
    }

    /// <summary>Creates envelope validation with a stage-specific payload limit.</summary>
    /// <param name="maximumPayloadBytes">Maximum serialized payload size accepted for the stage.</param>
    /// <returns>A validation rule set using the supplied limit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the payload limit is not positive.</exception>
    public static StrategyStageResultEnvelopeValidationRules WithMaximumPayloadBytes(int maximumPayloadBytes)
        => new(maximumPayloadBytes);

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
            RuleFor(x => x).Must(static envelope => envelope.HasContent).WithMessage(PayloadRequiredErrorMessage);
            RuleFor(x => x.ContentSize)
                .Must(size => size <= maximumPayloadBytes)
                .WithMessage(PayloadLimitErrorMessage);
            RuleFor(x => x).Must(static envelope => envelope.HasValidPayloadSha256())
                .WithMessage(PayloadHashErrorMessage);
        }
    }
}
