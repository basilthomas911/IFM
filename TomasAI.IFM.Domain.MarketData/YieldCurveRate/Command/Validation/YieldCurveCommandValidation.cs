using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Validation;

/// <summary>Accumulates Yield Curve command payload validation outside the actor.</summary>
public static class YieldCurveCommandValidation
{
    private static readonly YieldCurveRateValidationRules Rules = new();

    /// <summary>Checks every rate model field, including missing models.</summary>
    public static TErrors ValidateYieldCurveCommand<TErrors>(this TErrors errors, AddYieldCurveRateCommand command)

        where TErrors : class, ICollection<ValidationError>
        
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        ValidateRate(errors, command.YieldCurveRate);
        return errors;
    }

    /// <summary>Checks every amended rate model field, including missing models.</summary>
    public static TErrors ValidateYieldCurveCommand<TErrors>(this TErrors errors, ChangeYieldCurveRateCommand command)

        where TErrors : class, ICollection<ValidationError>
        
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        ValidateRate(errors, command.YieldCurveRate);
        return errors;
    }

    /// <summary>Checks the import request date.</summary>
    public static TErrors ValidateYieldCurveCommand<TErrors>(this TErrors errors, ImportYieldCurveRatesCommand command)

        where TErrors : class, ICollection<ValidationError>
        
    {
        ArgumentNullException.ThrowIfNull(command);
        return errors.ValidateDateTime(command.ImportDate, command.CommandName, "ImportDate");
    }

    /// <summary>Adapts shared model rules without throwing for a null payload.</summary>
    private static void ValidateRate(ICollection<ValidationError> errors,YieldCurveRateReadModel? rate)
    {
        if (rate is null) errors.Add(new("YieldCurveRate is required."));
        else
            foreach (var validationFailure in Rules.Execute(rate))
                errors.Add(validationFailure);
    }
}
