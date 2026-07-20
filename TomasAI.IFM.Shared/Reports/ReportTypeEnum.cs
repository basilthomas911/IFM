using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Reports
{
    public enum ReportTypeEnum
    {
        Unknown,
        DailyPosition,
        MonthlyNetAssetValue
    }

    public static class ReportTypeEnumExtensions
    {
        public static string ToStringFast(this ReportTypeEnum value) => value switch
        {
            ReportTypeEnum.Unknown => nameof(ReportTypeEnum.Unknown),
            ReportTypeEnum.DailyPosition => nameof(ReportTypeEnum.DailyPosition),
            ReportTypeEnum.MonthlyNetAssetValue => nameof(ReportTypeEnum.MonthlyNetAssetValue),
            _ => value.ToString()
        };
    }
}
