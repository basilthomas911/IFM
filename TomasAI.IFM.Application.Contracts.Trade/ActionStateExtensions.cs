using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class ActionStateExtensions
    {

        extension(ActionState value)
        {
            public string ToStringFast() => value switch
            {
                ActionState.Normal => nameof(ActionState.Normal),
                ActionState.Warning => nameof(ActionState.Warning),
                ActionState.Critical => nameof(ActionState.Critical),
                ActionState.RedAlert => nameof(ActionState.RedAlert),
                _ => value.ToString()
            };
        }
    }
}
