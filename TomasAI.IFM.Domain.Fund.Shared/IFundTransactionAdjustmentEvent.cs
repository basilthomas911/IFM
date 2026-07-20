using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// 
/// </summary>
public interface IFundTransactionAdjustmentEvent
{
     FundTransactionReadModel FundTransaction { get; init; }
     string CreatedBy { get; init; }
     DateTime CreatedOn { get; init; }

}
