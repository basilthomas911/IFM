using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit.Validation;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit.Decorators;

public class TradePlanForwardLossLimitCommandDecorator
    : BaseValidationDecorator<TradePlanForwardLossLimitBoundedContextState>,
        IValidate<UpdateTradePlanForwardLossLimitCommand>,
        IValidate<ClearTradePlanForwardLossLimitCommand>
{
    /// <summary>
    /// Validates the specified <see cref="UpdateTradePlanForwardLossLimitCommand"/> instance for correctness.
    /// </summary>
    /// <remarks>This method performs a series of validations on the provided command, including checks for a
    /// valid command ID,  command name, and trade plan forward loss limit. If any validation errors are found, a  <see
    /// cref="CommandValidationException"/> is thrown with details about the errors.</remarks>
    /// <param name="e">The command to validate, containing the data to be checked.</param>
    public void ValidateCommand(UpdateTradePlanForwardLossLimitCommand e)
        => new List<ValidationError>()
            .ValidateCommandId(e.CommandId, e.CommandName)
            .ValidateTradePlanForwardLossLimit(e.TradePlanForwardLossLimit)
            .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// Validates the specified <see cref="ClearTradePlanForwardLossLimitCommand"/> instance for correctness.
    /// </summary>
    /// <remarks>This method ensures that the provided command meets all required validation criteria. If any
    /// validation errors are found, a <see cref="CommandValidationException"/> is thrown with the relevant error
    /// details.</remarks>
    /// <param name="e">The command to validate. Must not be <see langword="null"/>.</param>
    public void ValidateCommand(ClearTradePlanForwardLossLimitCommand e)
        => new List<ValidationError>()
            .ValidateCommandId(e.CommandId, e.CommandName)
            .ValidateTradePlanForwardLossLimitId(e.EntityId, e.CommandName)
            .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

}
