using System.Runtime.CompilerServices;
using Xunit;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

/// <summary>Guards the complete audited repository boundary alongside runtime replay tests.</summary>
public sealed class CreationSnapshotConventionTests
{
    [Fact]
    public void All_72_audited_repositories_declare_creation_loading_or_an_explicit_adapter()
    {
        var root = RepositoryRoot();
        Assert.Equal(72, Paths.Length);
        foreach (var relative in Paths)
        {
            var source = File.ReadAllText(Path.Combine(root, relative));
            Assert.True(source.Contains("FromCreation", StringComparison.Ordinal)
                || source.Contains("PortfolioOperationCreationSnapshot", StringComparison.Ordinal)
                || source.Contains("DatabaseOperationStateRepository", StringComparison.Ordinal), relative);
            Assert.DoesNotContain("LoadStateFromSnapshotAsync<", source);
            Assert.DoesNotContain("LoadStateFromSnapshotLastNRangeAsync<", source);
            Assert.DoesNotContain("MapReduceActorEventStreamAsync<", source);
        }
        var backup = File.ReadAllText(Path.Combine(root, "TomasAI.IFM.Domain.SystemAdmin/Shared/DatabaseBackup/Persistence/DatabaseOperationStateRepository.cs"));
        Assert.Contains("MapReduceActorEventStreamFromCreationAsync", backup);
    }

    static string RepositoryRoot([CallerFilePath] string source = "")
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(source)!); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.Domain.Reference"))) return directory.FullName;
        throw new DirectoryNotFoundException("IFM source root not found.");
    }

    static readonly string[] Paths =
    [
        "TomasAI.IFM.Domain.Application/Command/State/ApplicationStateRepository.cs",
        "TomasAI.IFM.Domain.BrokerAccount/Command/State/BrokerAccountStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesAdxSignal/Command/State/FuturesAdxSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesAtrSignal/Command/State/FuturesAtrSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesBbSignal/Command/State/FuturesBbSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesEmaSignal/Command/State/FuturesEmaSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Command/State/FuturesItiSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesMacdSignal/Command/State/FuturesMacdSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesRsiSignal/Command/State/FuturesRsiSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesTdiSignal/Command/State/FuturesTdiSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesTradeSessionBarSignal/Command/State/FuturesTradeSessionBarSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesTradeSignal/Command/State/FuturesTradeSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesVwapSignal/Command/State/FuturesVwapSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/FuturesVxTermStructureSignal/Command/State/FuturesVxTermStructureSignalStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/HistoricalDataLoader/Command/State/FuturesAnalyticsHistoricalDataLoaderStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Command/State/MarketOutlookSnapshotStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Feed/Recovery/Function/State/MarketDataFeedRecoveryRecordRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Feed/Command/State/MarketDataFeedStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Feed/FuturesBarData/Command/State/FuturesBarDataStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Feed/FuturesClosingPrice/Command/State/FuturesClosingPriceStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Feed/FuturesEodData/Command/State/FuturesEodDataStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Command/State/FuturesOptionTickDataStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Feed/FuturesTickData/Command/State/FuturesTickDataStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Feed/Recovery/Command/State/MarketDataFeedRecoveryStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Securities/FuturesContract/Command/State/FuturesContractStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData.Securities/FuturesOptionContract/Command/State/FuturesOptionContractStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData/DownloadLog/Command/State/DownloadLogStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData/EconomicCalendar/Command/State/EconomicCalendarStateRepository.cs",
        "TomasAI.IFM.Domain.MarketData/YieldCurveRate/Command/State/YieldCurveRateStateRepository.cs",
        "TomasAI.IFM.Domain.Portfolio/CapacityReservation/Function/State/CapacityConsumptionFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Portfolio/CapacityReservation/Function/State/CapacityReservationFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Portfolio/OrderComposition/Function/State/PortfolioCloseOrderCompositionFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Portfolio/OrderComposition/Function/State/PortfolioOrderCompositionFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Reference/Configuration/Strategy/Command/State/RegimeDiscoveryConfigurationStateRepository.cs",
        "TomasAI.IFM.Domain.Reference/LookupType/Command/State/LookupTypeStateRepository.cs",
        "TomasAI.IFM.Domain.Reference/ParameterSets/Assignment/Command/State/ParameterAssignmentStateRepository.cs",
        "TomasAI.IFM.Domain.Reference/ParameterSets/Command/State/ParameterSetStateRepository.cs",
        "TomasAI.IFM.Domain.Reference/ParameterSets/Startup/Command/State/ParameterStartupStateRepository.cs",
        "TomasAI.IFM.Domain.Supervisor/Operations/Command/State/SupervisorStateRepository.cs",
        "TomasAI.IFM.Domain.Supervisor/Recovery/Command/State/RecoveryCanaryStateRepository.cs",
        "TomasAI.IFM.Domain.SystemAdmin/DatabaseBackup/BackupSet/Command/State/DatabaseBackupSetStateRepository.cs",
        "TomasAI.IFM.Domain.SystemAdmin/DatabaseBackup/Backup/Command/State/DatabaseBackupStateRepository.cs",
        "TomasAI.IFM.Domain.SystemAdmin/DatabaseBackup/Policy/Command/State/DatabaseBackupPolicyStateRepository.cs",
        "TomasAI.IFM.Domain.SystemAdmin/DatabaseBackup/Recovery/Command/State/DatabaseRecoveryStateRepository.cs",
        "TomasAI.IFM.Domain.SystemAdmin/DatabaseBackup/Retention/Command/State/DatabaseBackupRetentionStateRepository.cs",
        "TomasAI.IFM.Domain.SystemAdmin/DatabaseBackup/Service/Command/State/DatabaseBackupServiceStateRepository.cs",
        "TomasAI.IFM.Domain.SystemAdmin/ScheduledTask/Catalog/Command/State/ScheduledTaskCatalogStateRepository.cs",
        "TomasAI.IFM.Domain.SystemAdmin/ScheduledTask/Command/State/ScheduledTaskStateRepository.cs",
        "TomasAI.IFM.Domain.SystemAdmin/ScheduledTask/Run/Command/State/ScheduledTaskRunStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Command/State/FuturesTradeStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Option/Command/State/FuturesOptionTradeStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Command/State/IronCondorPositionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/ForwardPrice/Function/State/IronCondorForwardPriceFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Plan/Function/State/IronCondorTradePlanFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Workflow/Command/State/IronCondorExitPositionWorkflowStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Option/Position/VerticalSpread/Command/State/VerticalSpreadPositionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Option/Position/VerticalSpread/Plan/Function/State/VerticalSpreadTradePlanFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Option/Position/VerticalSpread/Workflow/Command/State/VerticalSpreadExitPositionWorkflowStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Position/Command/State/FuturesPositionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Position/Plan/Function/State/FuturesTradePlanFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Futures/Position/Workflow/Command/State/FuturesExitPositionWorkflowStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Model/Position/Workflow/Function/State/ExitOrderCompositionFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Model/Position/Workflow/Function/State/PositionExitRiskFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Order/Broker/Command/State/BrokerOrderStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Order/Command/State/TradeOrderStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Order/Execution/Command/State/OrderExecutionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/Command/State/IntrinsicTimeStrategyWorkflowStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/MarketCondition/Function/State/MarketConditionAssessmentStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Function/State/OrderCompositionFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RegimeDiscovery/Function/State/RegimeDiscoveryFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RiskManager/Function/State/RiskManagementFunctionStateRepository.cs",
        "TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/TradeSelection/Function/State/TradeSelectionFunctionStateRepository.cs",
    ];
}
