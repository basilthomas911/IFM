using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;
public sealed record DevelopmentTradingPortfolioReport(int PortfolioId, int PolicyId, IReadOnlyDictionary<TimeFrameType, int> FundIds, decimal DevelopmentCapital, int PublishedDeployments, bool Created, bool FinancialAuthorityReady);
