using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// 
/// </summary>
public enum OrderStatus
{
    Open,
    Cancelled,
    Completed,
    Closed
}

public static class OrderStatusExtensions
{
    public static string ToStringFast(this OrderStatus value) => value switch
    {
        OrderStatus.Open => nameof(OrderStatus.Open),
        OrderStatus.Cancelled => nameof(OrderStatus.Cancelled),
        OrderStatus.Completed => nameof(OrderStatus.Completed),
        OrderStatus.Closed => nameof(OrderStatus.Closed),
        _ => value.ToString()
    };
}
