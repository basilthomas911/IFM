using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Validation;

/// <summary>Validates the complete batch and its single aggregate route before loading or mutating state.</summary>
public static class SeedFuturesEmaSignalsValidation
{
    extension<TErrors>(TErrors errors) where TErrors : class, ICollection<ValidationError>
    {
        public TErrors ValidateSeedBatch(SeedFuturesEmaSignalsCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (command.Observations is not { Length: >= 1 and <= 64 })
            { errors.Add(new("EMA.SEED.COUNT", "An ordered seed must contain 1..64 observations.")); return errors; }
            DateTimeOffset? previous = null;
            foreach (var observation in command.Observations)
            {
                errors.ValidateSourceBar(new GenerateFuturesEmaSignalCommand
                { CommandId = command.CommandId, Subject = command.Subject, EntityId = command.EntityId, Observation = observation! });
                if (observation is null) continue;
                if (previous is { } end && observation.IntervalEndUtc <= end)
                    errors.Add(new("EMA.SEED.ORDER", "Seed intervals must be unique and strictly chronological."));
                previous = observation.IntervalEndUtc;
            }
            return errors;
        }
    }
}
