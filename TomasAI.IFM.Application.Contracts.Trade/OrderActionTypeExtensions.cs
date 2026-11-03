using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class OrderActionTypeExtensions
    {

        extension(OrderActionType value)
        {
            public string ToStringFast() => value switch
            {
                OrderActionType.Open => nameof(OrderActionType.Open),
                OrderActionType.Close => nameof(OrderActionType.Close),
                _ => value.ToString()
            };
        }
    }
}
