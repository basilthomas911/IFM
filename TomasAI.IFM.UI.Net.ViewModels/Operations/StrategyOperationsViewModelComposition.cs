using TomasAI.IFM.UI.Net.Services.Analytics;
namespace TomasAI.IFM.UI.Net.ViewModels.Operations;
/// <summary>Creates the existing framework-neutral view model without a host-wide navigation root.</summary>
public static class StrategyOperationsViewModelComposition
{
 public static StrategyOperationsViewModel Create(StrategyOperationsService service,string symbol,string contractId,DateOnly valueDate,TimeProvider? timeProvider=null) => new(service,symbol,contractId,valueDate,timeProvider);
}
