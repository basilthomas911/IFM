using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.PredictiveModel.FuturesItiTrend.Model
{
    public enum TrendModelState
    {
        None = 0,
        BuildStarted=1,
        ModelDataLoaded = 2,
        ModelTrained = 3,
        ModelLoaded = 4
    }

    public static class TrendModelStateExtensions
    {
        public static string ToStringFast(this TrendModelState value) => value switch
        {
            TrendModelState.None => nameof(TrendModelState.None),
            TrendModelState.BuildStarted => nameof(TrendModelState.BuildStarted),
            TrendModelState.ModelDataLoaded => nameof(TrendModelState.ModelDataLoaded),
            TrendModelState.ModelTrained => nameof(TrendModelState.ModelTrained),
            TrendModelState.ModelLoaded => nameof(TrendModelState.ModelLoaded),
            _ => value.ToString()
        };
    }
}
