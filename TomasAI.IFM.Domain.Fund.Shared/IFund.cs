using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Fund.Shared
{
    public interface IFund
    {
        int FundId { get; }
        string Name { get; }
        string Description { get; }
        DateTime CreatedOn { get; }
        string CreatedBy { get; }
        IFundOrderCollection Orders { get; }
        decimal Balance { get;}
    }
}
