using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Validation;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Validation;

public static  class OptionTradeValidationExtensions
{
    /// <summary>
    /// Validate option trade data model against validation rules.
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateParameters(this List<ValidationError> validationErrors, ChangeOptionTradeLegDataCommand e)
    {
        var commandValidationRules = new ChangeOptionTradeLegDataCommandValidationRules();
        var parameterRuleErrors = commandValidationRules.Execute(e);
        if (parameterRuleErrors is not null)
            validationErrors.AddRange(parameterRuleErrors);

        var validationRules = new OptionLegDataValidationRules();
        var ruleErrors = validationRules.Execute(e.OptionLegData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validate option trade data model against validation rules.
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateParameters(this List<ValidationError> validationErrors, ProcessOptionTradeEndOfDayCommand e)
    {
        var validationRules = new ProcessOptionTradeEndOfDayCommandValidationRules();
        var ruleErrors = validationRules.Execute(e);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validate option trade spread data model against validation rules.
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateOptionTradeSpreadData(this List<ValidationError> validationErrors, OptionTradeSpreadsDataModel e)
    {
        var validationRules = new OptionTradeSpreadsDataValidationRules();
        var ruleErrors = validationRules.Execute(e);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validate option trade spread bar data model against validation rules.
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateOptionTradeSpreadBarData(this List<ValidationError> validationErrors, OptionTradeSpreadBarsDataModel e)
    {
        var validationRules = new OptionTradeSpreadBarDataValidationRules();
        var ruleErrors = validationRules.Execute(e);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

}
