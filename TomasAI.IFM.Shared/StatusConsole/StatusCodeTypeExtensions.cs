using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.StatusConsole;

public static class StatusCodeTypeExtensions
{
    public static string ToStringFast(this StatusCodeType value) => value switch
    {
        StatusCodeType.Ok => nameof(StatusCodeType.Ok),
        StatusCodeType.Error => nameof(StatusCodeType.Error),
        _ => value.ToString()
    };
}
