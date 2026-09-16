using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Architecture;

/// <summary>Checks shared command ingress validation for optional closed observations.</summary>
public sealed class ClosedObservationIngressValidationTests
{
    [Fact]
    public void ValidClosedObservationAddsNoErrors()
    {
        var observation = SampleData.AtrObservation;
        var errors = new List<ValidationError>().ValidateClosedObservation(
            observation, observation.ContractId, observation.ValueDate,
            observation.TimeFrame, "GenerateAtr");

        Assert.Empty(errors);
    }

    [Fact]
    public void HistoricalSeedAllowsPriorValueDateButNeverAFutureValueDate()
    {
        var observation = SampleData.AtrObservation;
        var currentValueDate = observation.ValueDate.AddDays(1);

        var priorErrors = new List<ValidationError>().ValidateClosedObservation(
            observation,
            observation.ContractId,
            currentValueDate,
            observation.TimeFrame,
            "GenerateAtr",
            allowPriorValueDate: true);
        var futureErrors = new List<ValidationError>().ValidateClosedObservation(
            observation with { ValueDate = currentValueDate.AddDays(1) },
            observation.ContractId,
            currentValueDate,
            observation.TimeFrame,
            "GenerateAtr",
            allowPriorValueDate: true);

        Assert.DoesNotContain(priorErrors, error => error.ErrorCode == "ANALYTICS.BAR.IDENTITY");
        Assert.Contains(futureErrors, error => error.ErrorCode == "ANALYTICS.BAR.IDENTITY");
    }
    [Fact]
    public void MismatchedOrUnidentifiedObservationIsRejectedBeforeCalculation()
    {
        var observation = SampleData.AtrObservation with
        {
            ContractId = "OTHER", ObservationId = default
        };
        var errors = new List<ValidationError>().ValidateClosedObservation(
            observation, SampleData.ContractId, observation.ValueDate,
            observation.TimeFrame, "GenerateAtr");

        Assert.Contains(errors, error => error.ErrorCode == "ANALYTICS.BAR.IDENTITY");
        Assert.Contains(errors, error => error.ErrorMessage.Contains("ObservationId"));
    }
}
