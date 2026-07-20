using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Decorators;

public static class TradeOrderValidationExtensions
{
    /// <summary>
    /// Validate trade order entity id
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="tradeOrderEntityId"></param>
    /// <param name="commandName"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateTradeOrderId(this List<ValidationError> validationErrors, TradeOrderEntityId tradeOrderEntityId, string commandName)
    {
        if (tradeOrderEntityId.OrderId < 1)
            validationErrors.Add(new ValidationError($"{commandName}.OrderId must be > 0"));
        if (tradeOrderEntityId.TradeId < 1)
            validationErrors.Add(new ValidationError($"{commandName}.TradeId must be > 0"));
        if (tradeOrderEntityId.ValueDate == DateOnly.MinValue || tradeOrderEntityId.ValueDate == DateOnly.MaxValue)
            validationErrors.Add(new ValidationError($"{commandName}.ValueDate must be valid date"));
        return validationErrors;
    }

    /// <summary>
    /// Validate order price
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="orderPrice"></param>
    /// <param name="commandName"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateOrderPrice(this List<ValidationError> validationErrors, decimal orderPrice, string commandName)
    {
        if (orderPrice <= 0m)
            validationErrors.Add(new ValidationError($"{commandName}.OrderPrice must be > 0"));
        return validationErrors;
    }

}
