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

    /// <param name="validationErrors">
    ///
    /// </param>
    extension(List<ValidationError> validationErrors)
    {
        /// <summary>
        /// validate trade ticket
        /// </summary>
        ///
        /// <param name="tradeOrder"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
        public List<ValidationError> ValidateTradeOrder(TradeOrderReadModel tradeOrder)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);
            var ruleErrors = new TradeOrderValidationRules().Execute(tradeOrder);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
            return validationErrors;
        }
    }
}
