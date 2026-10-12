using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Reports
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2344", Justification = "Preserve the established serialized report enum type name.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "The established public actor queue/handler contract name describes its role and must remain compatible.")]
    public enum ReportTypeEnum
    {
        Unknown,
        DailyPosition,
        MonthlyNetAssetValue
    }
}
