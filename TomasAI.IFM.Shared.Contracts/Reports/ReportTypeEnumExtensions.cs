using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Reports
{

    public static class ReportTypeEnumExtensions
    {

        extension(ReportTypeEnum value)
        {
            public string ToStringFast() => value switch
            {
                ReportTypeEnum.Unknown => nameof(ReportTypeEnum.Unknown),
                ReportTypeEnum.DailyPosition => nameof(ReportTypeEnum.DailyPosition),
                ReportTypeEnum.MonthlyNetAssetValue => nameof(ReportTypeEnum.MonthlyNetAssetValue),
                _ => value.ToString()
            };
        }
    }
}
