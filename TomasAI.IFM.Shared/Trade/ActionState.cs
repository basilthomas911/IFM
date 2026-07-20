using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum ActionState
    {
        Normal,
        Warning,
        Critical,
        RedAlert
    }

    public static class ActionStateExtensions
    {
        public static string ToStringFast(this ActionState value) => value switch
        {
            ActionState.Normal => nameof(ActionState.Normal),
            ActionState.Warning => nameof(ActionState.Warning),
            ActionState.Critical => nameof(ActionState.Critical),
            ActionState.RedAlert => nameof(ActionState.RedAlert),
            _ => value.ToString()
        };
    }
}
