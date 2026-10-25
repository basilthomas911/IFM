using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Validation;

public static class TradeOrderValidationExtensions
{
    /// <summary>
    /// validate trade ticket
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="tradeOrder"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateTradeOrder(this List<ValidationError> validationErrors, TradeOrderReadModel tradeOrder)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        var ruleErrors = new TradeOrderValidationRules().Execute(tradeOrder);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}
