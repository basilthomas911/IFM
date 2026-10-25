using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class OrderStatusExtensions
    {
        public static string ToStringFast(this OrderStatus value) => value switch
        {
            OrderStatus.Placed => nameof(OrderStatus.Placed),
            OrderStatus.WaitingForFills => nameof(OrderStatus.WaitingForFills),
            OrderStatus.PartiallyFilled => nameof(OrderStatus.PartiallyFilled),
            OrderStatus.Filled => nameof(OrderStatus.Filled),
            OrderStatus.Cancelled => nameof(OrderStatus.Cancelled),
            OrderStatus.Executed => nameof(OrderStatus.Executed),
            OrderStatus.Error => nameof(OrderStatus.Error),
            _ => value.ToString()
        };
    }
}
