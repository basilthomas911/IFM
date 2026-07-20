using TomasAI.IFM.Shared.Trade.Validation;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.TradeAlgorithm.Commands;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.ValidationRules;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Decorators;

public static class OptionTradeAlgorithmValidationExtensions
{

    public static List<ValidationError> ValidateParameters(this List<ValidationError> validationErrors, ExecuteLongIronCondorAlgorithmCommand e)
    {
        var ruleErrors = new ExecuteLongIronCondorAlgorithmCommandValidationRules().Execute(e);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    public static List<ValidationError> ValidateParameters(this List<ValidationError> validationErrors, ExecuteShortIronCondorAlgorithmCommand e)
    {
        var ruleErrors = new ExecuteShortIronCondorAlgorithmCommandValidationRules().Execute(e);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    public static List<ValidationError>  ValidateOptionTrades(this List<ValidationError> validationErrors, IOptionTradeCollection? optionTrades)
    {
        if (optionTrades is null)
        {
            validationErrors.Add(new ValidationError("OptionTrades is empty"));
        }
        else
        {
            foreach (var e in optionTrades)
            {
                var ruleErrors = new OptionTradeValidationRules().Execute(e);
                if (ruleErrors is not null)
                    validationErrors.AddRange(ruleErrors);
            }
        }
        return validationErrors;
    }
}
