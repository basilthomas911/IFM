using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.StatusConsole;

public static class StatusCodeTypeExtensions
{

    extension(StatusCodeType value)
    {
        public string ToStringFast() => value switch
        {
            StatusCodeType.Ok => nameof(StatusCodeType.Ok),
            StatusCodeType.Error => nameof(StatusCodeType.Error),
            _ => value.ToString()
        };
    }
}
