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
