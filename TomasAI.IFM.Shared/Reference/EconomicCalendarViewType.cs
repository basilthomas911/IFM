using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Reference
{
    public enum EconomicCalendarViewType
    {
        Today,
        Yesterday,
        Tomorrow,
        ThisWeek,
        NextWeek
    }

    public static class EconomicCalendarViewTypeExtensions
    {
        public static string ToStringFast(this EconomicCalendarViewType value) => value switch
        {
            EconomicCalendarViewType.Today => nameof(EconomicCalendarViewType.Today),
            EconomicCalendarViewType.Yesterday => nameof(EconomicCalendarViewType.Yesterday),
            EconomicCalendarViewType.Tomorrow => nameof(EconomicCalendarViewType.Tomorrow),
            EconomicCalendarViewType.ThisWeek => nameof(EconomicCalendarViewType.ThisWeek),
            EconomicCalendarViewType.NextWeek => nameof(EconomicCalendarViewType.NextWeek),
            _ => value.ToString()
        };
    }
}
