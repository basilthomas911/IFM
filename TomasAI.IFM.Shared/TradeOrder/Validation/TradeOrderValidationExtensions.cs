using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.TradeOrder.Validation;

public static class TradeOrderValidationExtensions
{
    /// <summary>
    /// validate trade ticket
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="tradeOrder"></param>
    public static List<ValidationError> ValidateTradeOrder(this List<ValidationError> validationErrors, TradeOrderReadModel tradeOrder)
    {
        var ruleErrors = new TradeOrderValidationRules().Execute(tradeOrder);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}
