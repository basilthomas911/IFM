using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.Fund.Shared
{
    public enum FundAccountType
    {
        Unknown,
        GeneralPartner,
        Accredited,
        NonAccredited
    }

    public static class FundAccountTypeExtensions
    {
        public static string ToStringFast(this FundAccountType value) => value switch
        {
            FundAccountType.Unknown => nameof(FundAccountType.Unknown),
            FundAccountType.GeneralPartner => nameof(FundAccountType.GeneralPartner),
            FundAccountType.Accredited => nameof(FundAccountType.Accredited),
            FundAccountType.NonAccredited => nameof(FundAccountType.NonAccredited),
            _ => value.ToString()
        };
    }
}
