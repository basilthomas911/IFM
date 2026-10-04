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
    public static List<ValidationError> ValidateYieldCurveCommand(this List<ValidationError> errors, AddYieldCurveRateCommand command)
        => ValidateRate(errors, command.YieldCurveRate);

    /// <summary>Checks every amended rate model field, including missing models.</summary>
    public static List<ValidationError> ValidateYieldCurveCommand(this List<ValidationError> errors, ChangeYieldCurveRateCommand command)
        => ValidateRate(errors, command.YieldCurveRate);

    /// <summary>Checks the import request date.</summary>
    public static List<ValidationError> ValidateYieldCurveCommand(this List<ValidationError> errors, ImportYieldCurveRatesCommand command)
        => errors.ValidateDateTime(command.ImportDate, command.CommandName, "ImportDate");

    /// <summary>Adapts shared model rules without throwing for a null payload.</summary>
    private static List<ValidationError> ValidateRate(List<ValidationError> errors, YieldCurveRateReadModel? rate)
    {
        if (rate is null) errors.Add(new("YieldCurveRate is required."));
        else errors.AddRange(Rules.Execute(rate));
        return errors;
    }
}
