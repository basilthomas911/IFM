using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.TradeAlgorithm.Commands;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Validation;
using TomasAI.IFM.Shared.MarketData.Validation;
using TomasAI.IFM.Shared.Reference.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Decorators;

public class OptionTradeAlgorithmCommandDecorator(
    IValidationRules<OptionTradeReadModel> optionTradeValidationRules,
    IValidationRules<FuturesContractV2ReadModel> futuresContractValidationRules,
    IValidationRules<FuturesTradeSignalV2ReadModel> futuresTradeSignalValidationRules,
    IReferenceLookupService refLookupService)
    : BaseValidationDecorator<OptionTradeAlgorithmBoundedContextState>,
    IValidate<ExecuteLongIronCondorAlgorithmCommand>,
    IValidate<ExecuteShortIronCondorAlgorithmCommand>
{
    readonly IValidationRules<OptionTradeReadModel> _optionTradeValidationRules = IsArgumentNull.Set(optionTradeValidationRules);
    readonly IValidationRules<FuturesContractV2ReadModel> _futuresContractValidationRules = IsArgumentNull.Set(futuresContractValidationRules);
    readonly IValidationRules<FuturesTradeSignalV2ReadModel> _futuresTradeSignalValidationRules = IsArgumentNull.Set(futuresTradeSignalValidationRules);

    /// <summary>
    /// snapshot trade command
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(ExecuteLongIronCondorAlgorithmCommand e)
        => new List<ValidationError>()
            .ValidateCommandId( e.CommandId, e.CommandName)
            .ValidateParameters(e)
            .ValidateOptionTrades(e.OptionTrades)
            .ValidateFuturesContract(e.FuturesContract!, refLookupService)
            .ValidateFuturesTradeSignal(e.FuturesTradeSignal!)
            .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// validate change option trade distribution statistics  command
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(ExecuteShortIronCondorAlgorithmCommand e)
        => new List<ValidationError>()
            .ValidateCommandId(e.CommandId, e.CommandName)
            .ValidateParameters(e)
            .ValidateOptionTrades(e.OptionTrades)
            .ValidateFuturesContract(e.FuturesContract!, refLookupService)
            .ValidateFuturesTradeSignal(e.FuturesTradeSignal!)
            .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// validate option trades
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="optionTrades"></param>
    void ValidateOptionTrades(List<ValidationError> validationErrors, IOptionTradeCollection? optionTrades)
    {
        if ( optionTrades is null)
        {
            validationErrors.Add(new ValidationError("OptionTrades is empty"));
        }
        else
        {
            foreach (var e in optionTrades)
            {
                var ruleErrors = _optionTradeValidationRules.Execute(e);
                if (ruleErrors is not null)
                    validationErrors.AddRange(ruleErrors);
            }
        }
    }

    /// <summary>
    /// validate futures contract
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="futuresContract"></param>
    void ValidateFuturesContract(List<ValidationError> validationErrors, FuturesContractV2ReadModel? futuresContract)
    {
        if (futuresContract is null)
        {
            validationErrors.Add(new("FuturesContract is empty"));
        }
        else
        {
            var ruleErrors = _futuresContractValidationRules.Execute(futuresContract);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
    }

    /// <summary>
    /// validate futures trade signal
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="futuresTradeSignal"></param>
    void ValidateFuturesTradeSignal(List<ValidationError> validationErrors, FuturesTradeSignalV2ReadModel? futuresTradeSignal)
    {
        if (futuresTradeSignal is null)
        {
            validationErrors.Add(new ValidationError("FuturesTradeSignal is empty"));
        }
        else
        {
            var ruleErrors = _futuresTradeSignalValidationRules.Execute(futuresTradeSignal);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
    }
}
