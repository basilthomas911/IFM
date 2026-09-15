using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Validation;

/// <summary>Checks a VX leg and calculation configuration before loading state.</summary>
public static class UpdateFuturesVxTermStructureSignalValidation
{
    /// <summary>Adds deterministic ingress errors for invalid VX observations.</summary>
    public static List<ValidationError> ValidateInputs(
        this List<ValidationError> errors, UpdateFuturesVxTermStructureSignalCommand command)
    {
        if (command.Observation is not { } leg)
            errors.Add(new("VX.LEG.NULL", "A VX leg observation is required."));
        else
        {
            var expected = leg.Leg == FuturesVxTermStructureLeg.Front
                ? command.EntityId.FrontContractId : command.EntityId.BackContractId;
            if (leg.Leg is not (FuturesVxTermStructureLeg.Front or FuturesVxTermStructureLeg.Back)
                || !string.Equals(leg.ContractId, expected, StringComparison.Ordinal)
                || leg.Expiry == default || leg.Price <= 0 || leg.SourceSequence < 0
                || leg.SourceTimestampUtc.Offset != TimeSpan.Zero
                || leg.StreamEpochId == Guid.Empty)
                errors.Add(new("VX.LEG.INVALID", "The VX leg identity, expiry, price, or source lineage is invalid."));
        }
        if (command.Configuration is not { } config
            || config.FlatEpsilon < 0 || config.MaximumSourceSkew < TimeSpan.Zero
            || string.IsNullOrWhiteSpace(config.ConfigurationId)
            || config.ConfigurationId != command.EntityId.ConfigurationId)
            errors.Add(new("VX.CONFIG.INVALID", "The VX configuration is invalid or does not match the stream."));
        if (command.Subject.EntityId != command.EntityId.Format())
            errors.Add(new("VX.ROUTE", "Command routing identity must match the VX stream."));
        return errors;
    }
}
