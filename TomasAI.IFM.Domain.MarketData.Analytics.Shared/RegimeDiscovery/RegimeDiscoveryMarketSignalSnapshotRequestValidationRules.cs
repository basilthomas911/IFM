using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Validates an exact Regime Discovery snapshot request.</summary>
public sealed class RegimeDiscoveryMarketSignalSnapshotRequestValidationRules
    : BaseValidationRules, IValidationRules<RegimeDiscoveryMarketSignalSnapshotRequest>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied request.</summary>
    /// <param name="value">Request to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(RegimeDiscoveryMarketSignalSnapshotRequest value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<RegimeDiscoveryMarketSignalSnapshotRequest>
    {
        public Validator()
        {
            RuleFor(x => x.MarketSeriesIdentity)
                .Must(x => new MarketSeriesIdentityValidationRules().Execute(x).Length == 0);
            RuleFor(x => x.TargetHorizon).Must(static value =>
                value is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly);
            RuleFor(x => x.Requirements).NotEmpty();
            RuleForEach(x => x.Requirements).ChildRules(requirement =>
            {
                requirement.RuleFor(x => x.Metric).IsInEnum().NotEqual(RegimeDiscoverySignalMetric.Unknown);
                requirement.RuleFor(x => x.TimeFrame).IsInEnum().NotEqual(TimeFrameType.None);
                requirement.RuleFor(x => x.CalculationConfigurationId).NotEmpty();
                requirement.RuleFor(x => x.MaximumAgeSeconds).GreaterThan(0);
                requirement.RuleFor(x => x.Weight).GreaterThanOrEqualTo(0m);
            });
            RuleFor(x => x.FutureClockSkewSeconds).GreaterThanOrEqualTo(0);
            RuleFor(x => x.SupportedSchemaVersions).NotEmpty();
            RuleFor(x => x.ApprovedCalculationVersions).NotEmpty();
            RuleFor(x => x.CaptureAttempts).InclusiveBetween(1, 10);
        }
    }
}
