using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using System.Security.Cryptography;
using FluentValidation;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

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
