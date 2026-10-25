using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Development-only source registry. Routing expressions are source evidence, not
// guessed executable recipes. Each pending operation still needs a native owner.
internal static class OperationInventory
{
    public static void Export(string output)
    {
        var repository = new DirectoryInfo(output);
        while (repository != null && !Directory.Exists(Path.Combine(repository.FullName,"TomasAI.IFM.Application.Api.Nats.Client"))) repository = repository.Parent;
        if(repository == null) throw new InvalidOperationException("Cannot find API client sources for operation inventory");
        var uiReferences=UiApiReferences.Scan(repository.FullName);
        var rows = new List<object>();
        foreach(var file in Directory.EnumerateFiles(Path.Combine(repository.FullName,"TomasAI.IFM.Application.Api.Nats.Client"),"*.cs").Order(StringComparer.Ordinal))
        {
            var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            foreach(var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>m.Modifiers.Any(SyntaxKind.PublicKeyword)))
            {
                var owner=method.Ancestors().OfType<TypeDeclarationSyntax>().First().Identifier.Text;
                var name=method.Identifier.Text;
                string? native=(owner,name) switch {
                    ("IntrinsicTimeStrategyWorkflowQueryApi","GetByIdAsync")=>"services/inspection_queries.rs; state/strategy_inspection.rs",
                    ("MarketConditionAssessmentQueryApi","GetAsync" or "HistoryAsync")=>"services/inspection_queries.rs; state/strategy_inspection.rs",
                    ("RiskQueryApi","GetInvocationAsync" or "GetHistoryAsync")=>"services/inspection_queries.rs; state/portfolio/risk_history.rs",
                    ("OrderExecutionQueryApi","GetAsync")=>"services/broker_reads.rs; state/trades/composer.rs",
                    ("PortfolioIdentityApi","AllocatePolicyIdAsync")=>"services/portfolio.rs; state/portfolio/policy.rs",
                    ("ParameterSetsApi","PreviewValuesAsync" or "ValidateValuesAsync" or "PreviewStartupAsync" or "StartupRunAsync" or "StartupRunsAsync" or "StartupReportAsync" or "SignalMonitoringAsync" or "LegacyVersionsAsync" or "PreviewLegacyMigrationAsync" or "ComponentsAsync" or "VersionsAsync" or "SchemaAsync" or "CreateAsync" or "SaveAsync" or "PublishAsync" or "StateAsync" or "RenameAsync" or "AssignmentAsync" or "AssignAsync" or "DisableAssignmentAsync" or "RetireAsync")=>"services/parameters.rs; state/parameters.rs",
                    ("PortfolioFinancialPolicyCommandApi","CreatePolicyAsync" or "AddPolicyVersionAsync" or "ActivateAndAssignAsync" or "RetirePolicyAsync" or "DeleteDraftPolicyAsync")=>"services/portfolio_commands.rs; state/portfolio/policy.rs",
                    ("TradeFeedRecoveryApi","LoadAsync" or "EnableAsync" or "DisableAsync")=>"services/trade_feed_recovery.rs; state/trade_monitors/commands.rs",
                    ("EstablishedTradeQueryApi","GetRealtimeAsync" or "GetAsync")=>"services/trade_monitor_queries.rs; state/trade_monitors/mod.rs",
                    ("StrategyPositionQueryApi","GetCurrentAsync" or "GetHistoryAsync")=>"services/trade_monitor_queries.rs; state/trade_monitors/data.rs",
                    ("TradeOrderLifecycleApi","SubmitAcceptedAsync")=>"services/order_submission.rs; state/trades/submissions.rs",
                    ("PortfolioQueryApi","GetPortfolioAsync" or "GetPortfolioRevisionAsync" or "GetPortfoliosAsync" or "GetFundAsync" or "GetFundRevisionAsync" or "GetFundsAsync" or "GetFundAllocationAsync" or "GetFundRiskEnvelopeAsync" or "GetAssignmentsAsync" or "GetOrderAsync" or "GetTradeAsync" or "GetCompositionByWorkflowAsync" or "GetOrdersAsync" or "GetOrderTradesAsync" or "GetStrategyReferenceCombinationsAsync" or "GetPolicyAsync" or "GetPoliciesAsync" or "GetActivePolicyAsync")=>"services/portfolio.rs; state/portfolio.rs; state/trades.rs",
                    ("PortfolioCommandApi","CreatePortfolioAsync" or "AddPortfolioVersionAsync" or "ChangePortfolioStateAsync" or "AddFundAsync" or "DelegateAllocationAsync" or "DelegateRiskEnvelopeAsync" or "RetirePortfolioAsync" or "DeleteDraftPortfolioAsync")=>"services/portfolio_commands.rs; state/portfolio.rs",
                    ("PortfolioFundCommandApi","CreateFundMandateAsync" or "AddFundMandateVersionAsync" or "ChangeFundStateAsync" or "AssignTradeTemplateAsync" or "CreateManualOrderAsync" or "AddManualTradeAsync" or "RemoveManualTradeAsync" or "DeleteManualOrderAsync")=>"services/portfolio_commands.rs; state/portfolio.rs; state/trades.rs",
                    ("PortfolioFinancialApi","PrepareFinancialAuthorityAsync" or "PrepareFinancialBookAsync" or "GetFinancialLedgerConfigurationAsync" or "GetFinancialPostingConfigurationAsync" or "GetFundRiskAuthorizationAsync" or "GetFinancialAdmissionSnapshotAsync" or "GetPostingReceiptAsync" or "GetJournalAsync" or "GetAccountBalancesAsync" or "GetTrialBalanceAsync" or "GetFundPerformanceMetricsAsync" or "GetFundTransactionsPageAsync" or "GetReconciliationAsync" or "GetCapacityReservationAsync" or "GetCapacityUsageAsync" or "GetFundReservationsPageAsync" or "PostAsync" or "ConfigureAsync" or "PostBatchAsync" or "ChangeAsync")=>"services/financial.rs; services/financial_operations.rs; state/portfolio/financial_ui.rs",
                    ("BrokerAccountQueryApi","GetAsync")=>"services/broker_reads.rs; state/trades.rs",
                    ("BrokerAccountCommandApi","SubmitQualificationEvidenceAsync" or "AcceptQualificationAsync" or "RevokeQualificationAsync" or "SetManualHoldAsync" or "ReleaseManualHoldAsync" or "RequestResynchronizationAsync")=>"services/broker_account_commands.rs; state/trades.rs",
                    ("BrokerOrderQueryApi","GetAsync" or "ListAsync")=>"services/broker_reads.rs; state/trades.rs",
                    ("BrokerOrderCommandApi","UpdatePriceAsync" or "CancelAsync")=>"services/broker_order_commands.rs; state/trades.rs",
                    ("OptionTradeQueryApi","GetTradeEndOfDayDisplayAsync")=>"services/trade_eod.rs; state/trades.rs",
                    ("OptionTradeQueryApi","PrepareTradeOrderAsync")=>"services/order_preparation.rs; state/trades.rs",
                    ("OptionTradeQueryApi","GetTradeOrderPreviewAsync")=>"services/broker_preview.rs; state/trades.rs",
                    ("StrategyTradePlanQueryApi","GetIronCondorChartAsync" or "GetCurrentIronCondorAsync" or "GetIronCondorHistoryPageAsync" or "GetIronCondorHistoryAsync")=>"services/trade_monitor_queries.rs; state/trade_monitors/mod.rs",
                    ("MarketDataQueryApi","GetValueDateAsync" or "GetMarketSessionAsync")=>"services/business_context.rs; app/runtime.rs",
                    ("MarketDataFeedQueryApi","GetRuntimeStatusAsync")=>"services/feed_status.rs",
                    ("MarketDataFeedCommandApi","StartMarketDataFeedAsync" or "StopMarketDataFeedAsync")=>"services/feed_control.rs",
                    ("MarketDataQueryApi","GetOnTheRunFuturesContractAsync")=>"services/market_history.rs",
                    ("MarketDataQueryApi","GetFuturesContractsAsync")=>"state/futures_contract.rs",
                    ("MarketDataCommandApi","AddFuturesContractAsync" or "ChangeFuturesContractAsync" or "RemoveFuturesContractAsync")=>"state/futures_contract.rs",
                    ("MarketDataQueryApi","GetFuturesOptionContractsPageAsync")=>"state/option_contract.rs",
                    ("MarketDataCommandApi","AddFuturesOptionContractAsync" or "ChangeFuturesOptionContractAsync" or "RemoveFuturesOptionContractAsync")=>"state/option_contract.rs",
                    ("MarketDataQueryApi","GetInstrumentDefinitionsAsync" or "GetFuturesContractAsync" or "GetFuturesOptionContractAsync")=>"state/provider_definitions.rs; services/provider_mapping.rs",
                    ("MarketDataFeedQueryApi","GetFuturesBarWindowAsync")=>"services/market_history.rs",
                    ("MarketDataAnalyticsQueryApi","GetFuturesBollingerBandHistoryAsync")=>"services/market_history.rs",
                    ("MarketDataAnalyticsQueryApi","GetMarketOutlookSnapshotAsync")=>"services/outlook.rs; services/market_history.rs",
                    ("ReferenceQueryApi","GetLookupTypesAsync") when method.ParameterList.Parameters.Count==0=>"services/reference.rs; state/lookup.rs",
                    ("ReferenceCommandApi","AddLookupTypeAsync" or "ChangeLookupTypeAsync" or "RemoveLookupTypeAsync")=>"state/lookup.rs",
                    ("ReferenceQueryApi","QueryStrategyCatalogAsync" or "GetTradeStrategySymbolsAsync")=>"state/strategy_catalog.rs",
                    ("ReferenceCommandApi","ExecuteStrategyCatalogAsync")=>"state/strategy_catalog.rs",
                    ("MarketDataQueryApi","GetYieldCurveRateYearsAsync" or "GetYieldCurveRatesAsync" or "YieldCurveRateExistsAsync")=>"state/yield_curve.rs",
                    ("MarketDataCommandApi","AddYieldCurveRateAsync" or "ChangeYieldCurveRateAsync" or "RemoveYieldCurveRateAsync" or "ImportYieldCurveRatesAsync")=>"state/yield_curve.rs",
                    ("MarketDataQueryApi","GetEconomicCalendarCountryCodesAsync")=>"state/calendar.rs",
                    ("MarketDataQueryApi","GetEconomicCalendarsAsync") when method.ParameterList.Parameters.Count==3=>"state/calendar.rs",
                    ("MarketDataCommandApi","AddEconomicCalendarAsync" or "ChangeEconomicCalendarAsync" or "RemoveEconomicCalendarAsync" or "ImportEconomicCalendarsAsync")=>"state/calendar.rs",
                    ("ScheduledTaskQueryApi","GetScheduledTasksDashboardAsync" or "GetScheduledTaskRunHistoryAsync" or "GetScheduledTaskOutputAsync" or "PreviewScheduledTaskScheduleAsync")=>"state/scheduled_tasks.rs",
                    ("ScheduledTaskCommandApi","CreateScheduledTaskAsync" or "ChangeScheduledTaskScheduleAsync" or "EnableScheduledTaskAsync" or "DisableScheduledTaskAsync" or "RemoveScheduledTaskAsync" or "RequestScheduledTaskRunAsync" or "FailScheduledTaskRunAsync")=>"state/scheduled_tasks.rs",
                    ("DatabaseBackupQueryApi","GetProtectionSetsAsync" or "ListBackupOperationsAsync" or "GetBackupSetupAsync" or "GetLatestVerifiedBackupAsync" or "GetLatestRestoreTestedBackupAsync" or "GetBackupLogAsync" or "GetPolicyAsync" or "GetServiceHealthAsync")=>"state/database_backup.rs; services/database_backup.rs",
                    ("DatabaseBackupCommandApi","RequestBackupAsync" or "CancelBackupAsync" or "RequestRestoreDrillAsync" or "UpdatePolicyAsync")=>"state/database_backup.rs; services/database_backup.rs",
                    ("SupervisorCommandApi","ExecuteActorOperationAsync")=>"state/actor_health.rs",
                    ("NatsClientApi","StartAsync" or "StopAsync")=>"backend/transport.rs; app/runtime.rs",
                    _=>null
                };
                if(native is not null)foreach(var source in native.Split("; ",StringSplitOptions.RemoveEmptyEntries))
                    if(!File.Exists(Path.Combine(repository.FullName,"TomasAI.IFM.UI.Rust.eGui","src",source)))
                        throw new InvalidDataException($"Native operation owner does not exist: {source}");
                rows.Add(new {
                    source=Path.GetRelativePath(repository.FullName,file).Replace('\\','/'),
                    line=tree.GetLineSpan(method.Span).StartLinePosition.Line+1,
                    client=owner,method=name,parameters=method.ParameterList.Parameters.Select(p=>new{name=p.Identifier.Text,type=p.Type?.ToString(),optional=p.Default?.Value.ToString()}).ToArray(),
                    result=method.ReturnType.ToString(),
                    calls=method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(i=>i.Expression.ToString()).Distinct().ToArray(),
                    subjects=method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Where(n=>n.Type.ToString().EndsWith("ActorSubject",StringComparison.Ordinal)).Select(n=>n.ArgumentList?.ToString()).ToArray(),
                    resolvedUiCallSites=uiReferences.GetValueOrDefault((owner,name,method.ParameterList.Parameters.Count),[]),
                    nativeOwner=native,status=native==null?"Pending reachability review or native port":"Implemented; see native contract/controller tests"
                });
            }
        }
        FixtureFiles.WriteAllText(Path.Combine(output,"operations.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Inventoried {rows.Count} public API client methods; pending rows are not declared migrated.");
    }
}
