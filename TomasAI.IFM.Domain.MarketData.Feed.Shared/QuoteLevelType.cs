using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero represents no valid persisted business choice; adding it would introduce an unsupported value.")]
    public enum QuoteLevelType
    {
        LevelOne = 1,
        LevelTwo = 2,
    }
}
