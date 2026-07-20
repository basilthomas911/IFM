using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Domain.Trade.Plan.Validation;

namespace TomasAI.IFM.Domain.Trade.Plan.Decorators;

public class TradePlanCommandDecorator() 
    : BaseValidationDecorator<TradePlanBoundedContextState>,
    IValidate<UpdateTradePlanCommand>
{
    /// <summary>
    /// validate update trade plan command
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(UpdateTradePlanCommand e)
        => new List<ValidationError>()
            .ValidateCommandId(e.CommandId, e.CommandName)
            .ValidateTradePlan(e.TradePlan)
            .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);
}
