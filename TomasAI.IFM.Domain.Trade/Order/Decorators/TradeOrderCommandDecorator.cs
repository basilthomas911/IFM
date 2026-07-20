using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Decorators;

public class TradeOrderCommandDecorator
     : BaseValidationDecorator<TradeOrderBoundedContextState>,
            IValidate<CancelOrderCommand>,
            IValidate<CloseOrderCommand>,
            IValidate<ExecuteCancelOrderCommand>,
            IValidate<ExecuteUpdateOrderCommand>,
            IValidate<FillOrderCommand>,
            IValidate<OpenOrderCommand>,
            IValidate<PlaceTradeOrderCommand>,
            IValidate<UpdateOrderCommand>
{
    /// <summary>
    /// Validate CancelOrderCommand
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(CancelOrderCommand e)
         => new List<ValidationError>()
           .ValidateCommandId(e.CommandId, e.CommandName)
           .ValidateTradeOrderId(e.TradeOrderId, e.CommandName)
           .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// Validate CloseOrderCommand
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(CloseOrderCommand e)
        => new List<ValidationError>()
           .ValidateCommandId(e.CommandId, e.CommandName)
           .ValidateTradeOrderId(e.EntityId, e.CommandName)
           .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// Validate ExecuteCancelOrderCommand
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(ExecuteCancelOrderCommand e)
        => new List<ValidationError>()
           .ValidateCommandId(e.CommandId, e.CommandName)
           .ValidateTradeOrderId(e.EntityId, e.CommandName)
           .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// Validate ExecuteUpdateOrderCommand
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(ExecuteUpdateOrderCommand e)
        => new List<ValidationError>()
           .ValidateCommandId(e.CommandId, e.CommandName)
           .ValidateTradeOrderId(e.EntityId, e.CommandName)
           .ValidateOrderPrice(e.OrderPrice, e.CommandName)
           .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// Validate FillOrderCommand
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(FillOrderCommand e)
        => new List<ValidationError>()
             .ValidateCommandId(e.CommandId, e.CommandName)
             .ValidateTradeOrderId(e.EntityId, e.CommandName)
             .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// Validate OpenOrderCommand
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(OpenOrderCommand e)
        => new List<ValidationError>()
             .ValidateCommandId(e.CommandId, e.CommandName)
             .ValidateTradeOrderId(e.EntityId, e.CommandName)
             .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// Validate PlaceTradeOrderCommand
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(PlaceTradeOrderCommand e)
         => new List<ValidationError>()
              .ValidateCommandId(e.CommandId, e.CommandName)
              .ValidateTradeOrderId(e.EntityId, e.CommandName)
              .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

    /// <summary>
    /// Validate UpdateOrderCommand
    /// </summary>
    /// <param name="e"></param>
    public void ValidateCommand(UpdateOrderCommand e)
         => new List<ValidationError>()
              .ValidateCommandId(e.CommandId, e.CommandName)
              .ValidateTradeOrderId(e.EntityId, e.CommandName)
              .ThrowCommandValidationExceptionOnAnyError(e.ErrorCode);

}
