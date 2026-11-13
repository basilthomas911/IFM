using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.FirstOrDefault() is "--sequence-context" or "--sequence-block-warm")
        {
            var focused = args[0] == "--sequence-block-warm";
            await new SequenceContextConventionBenchmarks { ColdConnection = false }.Setup();
            if (!focused) await new SequenceContextConventionBenchmarks { ColdConnection = true }.Setup();
            // In-process measurement avoids recursively discovering unrelated archived project
            // copies under the repository's artifact junction. Each arm still starts a new
            // Release host and uses the same isolated, fully durable database configuration.
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(focused ? 6 : 3).WithIterationCount(focused ? 20 : 8).WithInvocationCount(focused ? 128 : 16).WithUnrollFactor(1));
            if (focused) configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                benchmark.Descriptor.WorkloadMethod.Name == nameof(SequenceContextConventionBenchmarks.AllocateBlock)
                && benchmark.Parameters.Items.Any(parameter => parameter.Name == "ColdConnection" && Equals(parameter.Value, false))));
            var summary = BenchmarkRunner.Run<SequenceContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != (focused ? 1 : 8))
                throw new InvalidOperationException("All eight sequence-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--backup-phase-progress-first")
        {
            var indexed = args.Skip(1).FirstOrDefault() switch
            {
                "baseline" => false,
                "indexed" => true,
                _ => throw new ArgumentException("Specify baseline or indexed.")
            };
            await BackupPhaseProgressFirstBenchmarks.PrepareAsync(indexed);
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("BackupPhaseProgressFirst").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(5).WithIterationCount(16).WithInvocationCount(48).WithUnrollFactor(1));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                benchmark.Parameters.Items.Any(parameter => Equals(parameter.Value, "Max"))
                || (indexed && benchmark.Parameters.Items.All(parameter => !Equals(parameter.Value, "Append")))));
            var summary = BenchmarkRunner.Run<BackupPhaseProgressFirstBenchmarks>(config: configuration, args: args.Skip(2).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != (indexed ? 9 : 5))
                throw new InvalidOperationException("All backup phase progress measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-backup-phase-prior-max")
        {
            await BackupPhasePagingBenchmarks.PrepareAsync(indexed: true, verifyApplied: true);
        }
        else if (args.FirstOrDefault() == "--backup-phase-prior-max")
        {
            var indexed = args.Skip(1).FirstOrDefault() switch
            {
                "baseline" => false,
                "indexed" => true,
                _ => throw new ArgumentException("Specify baseline or indexed.")
            };
            await BackupPhasePagingBenchmarks.PrepareAsync(indexed);
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("BackupPhasePriorMax").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(12).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<BackupPhasePagingBenchmarks>(config: configuration, args: args.Skip(2).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four backup phase measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-system-admin-operation-paging")
        {
            await SystemAdminOperationPagingBenchmarks.PrepareAsync(indexed: true, verifyApplied: true);
        }
        else if (args.FirstOrDefault() == "--system-admin-operation-paging")
        {
            var indexed = args.Skip(1).FirstOrDefault() switch
            {
                "baseline" => false,
                "indexed" => true,
                _ => throw new ArgumentException("Specify baseline or indexed.")
            };
            await SystemAdminOperationPagingBenchmarks.PrepareAsync(indexed);
            var job = Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithUnrollFactor(1);
            var configuration = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(job.WithId("OperationPaging").WithInvocationCount(indexed ? 64 : 8));
            var summary = BenchmarkRunner.Run<SystemAdminOperationPagingBenchmarks>(config: configuration, args: args.Skip(2).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 8)
                throw new InvalidOperationException("All eight operation paging measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--system-admin-context")
        {
            await new SystemAdminContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new SystemAdminContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<SystemAdminContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 10)
                throw new InvalidOperationException("All ten SystemAdmin-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--scheduled-task-context")
        {
            await new ScheduledTaskContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new ScheduledTaskContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<ScheduledTaskContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 16)
                throw new InvalidOperationException("All sixteen scheduled-task-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() is "--securities-option-page-index" or "--securities-option-page-query-split")
        {
            var querySplit = args[0] == "--securities-option-page-query-split";
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("SecuritiesOptionPageIndex").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(12).WithInvocationCount(64).WithUnrollFactor(1));
            if (querySplit)
                configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                    benchmark.Descriptor.WorkloadMethod.Name != nameof(SecuritiesOptionPageIndexBenchmarks.InsertContract)));
            var summary = BenchmarkRunner.Run<SecuritiesOptionPageIndexBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != (querySplit ? 4 : 5))
                throw new InvalidOperationException("All option-page measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--securities-context")
        {
            await new SecuritiesContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new SecuritiesContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<SecuritiesContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 6)
                throw new InvalidOperationException("All six Securities-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-parameter-version-per-set-probes")
        {
            await ParameterVersionPagingBenchmarks.PrepareAsync();
        }
        else if (args.FirstOrDefault() == "--parameter-version-per-set-probes")
        {
            await ParameterVersionPagingBenchmarks.PrepareAsync();
            var job = Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithUnrollFactor(1);
            var configuration = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(job.WithId("ParameterPageBaseline").WithInvocationCount(12),
                    job.WithId("ParameterPageProbe").WithInvocationCount(96));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                (benchmark.Descriptor.WorkloadMethod.Name == nameof(ParameterVersionPagingBenchmarks.Current)) ==
                (benchmark.Job.Id == "ParameterPageBaseline")));
            var summary = BenchmarkRunner.Run<ParameterVersionPagingBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 8)
                throw new InvalidOperationException("All eight parameter version page measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-watchdog-status-specific")
        {
            await WatchdogStatusBenchmarks.PrepareAsync();
        }
        else if (args.FirstOrDefault() == "--verify-scheduled-task-history-split")
        {
            await ScheduledTaskHistoryBenchmarks.PrepareAsync();
        }
        else if (args.FirstOrDefault() == "--scheduled-task-history-split")
        {
            await ScheduledTaskHistoryBenchmarks.PrepareAsync();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ScheduledTaskHistorySplit").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(96).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<ScheduledTaskHistoryBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four scheduled task history measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-risk-history-bounded-ranges")
        {
            await RiskHistoryPagingBenchmarks.PrepareAsync();
        }
        else if (args.FirstOrDefault() == "--risk-history-bounded-ranges")
        {
            await RiskHistoryPagingBenchmarks.PrepareAsync();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("RiskHistoryBoundedRanges").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(64).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<RiskHistoryPagingBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four risk history measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--watchdog-status-specific")
        {
            await WatchdogStatusBenchmarks.PrepareAsync();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("WatchdogStatusSpecific").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(96).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<WatchdogStatusBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 6)
                throw new InvalidOperationException("All six watchdog status measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-watchdog-observation-filter-split")
        {
            await WatchdogObservationFilterBenchmarks.PrepareFixture();
        }
        else if (args.FirstOrDefault() == "--watchdog-observation-filter-split")
        {
            await WatchdogObservationFilterBenchmarks.PrepareFixture();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("WatchdogObservationFilterSplit").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(64).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<WatchdogObservationFilterBenchmarks>(config: configuration,
                args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 8)
                throw new InvalidOperationException("All eight watchdog observation measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-recovery-log-filter-migration")
        {
            await RecoveryLogIndexBenchmarks.PrepareAsync(withIndex: true, verifyMigration: true, splitExperiment: true, withDatasetIndex: true);
        }
        else if (args.FirstOrDefault() is "--recovery-log-filter-split" or "--recovery-log-dataset-write")
        {
            var writeOnly = args[0] == "--recovery-log-dataset-write";
            var withDatasetIndex = args.Skip(1).FirstOrDefault() switch
            {
                "baseline" => false,
                "candidate" => true,
                _ => throw new ArgumentException("Specify baseline or candidate.")
            };
            await RecoveryLogIndexBenchmarks.PrepareAsync(withIndex: true, splitExperiment: !writeOnly, withDatasetIndex: withDatasetIndex);
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("RecoveryLogFilterSplit").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(32).WithUnrollFactor(1));
            if (writeOnly)
                configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                    benchmark.Descriptor.WorkloadMethod.Name == nameof(RecoveryLogIndexBenchmarks.AppendRecovery)));
            else if (withDatasetIndex)
                configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                    benchmark.Parameters.Items.Any(parameter => Equals(parameter.Value, "Dataset"))));
            var benchmarkArgs = args.Skip(2).ToArray();
            var summary = writeOnly
                ? BenchmarkRunner.Run<RecoveryLogIndexBenchmarks>(config: configuration, args: benchmarkArgs)
                : BenchmarkRunner.Run<RecoveryLogFilterBenchmarks>(config: configuration, args: benchmarkArgs);
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != (writeOnly ? 1 : withDatasetIndex ? 4 : 16))
                throw new InvalidOperationException("All requested recovery-log filter measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-recovery-log-index-migration")
        {
            await RecoveryLogIndexBenchmarks.PrepareAsync(withIndex: true, verifyMigration: true);
        }
        else if (args.FirstOrDefault() is "--recovery-log-index" or "--recovery-log-index-write-check")
        {
            var writeOnly = args[0] == "--recovery-log-index-write-check";
            var withIndex = args.Skip(1).FirstOrDefault() switch
            {
                "baseline" => false,
                "candidate" => true,
                _ => throw new ArgumentException("Specify baseline or candidate.")
            };
            await RecoveryLogIndexBenchmarks.PrepareAsync(withIndex);
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("RecoveryLogIndex").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(5).WithIterationCount(20).WithInvocationCount(64).WithUnrollFactor(1));
            if (writeOnly)
                configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                    benchmark.Descriptor.WorkloadMethod.Name == nameof(RecoveryLogIndexBenchmarks.AppendRecovery)));
            var summary = BenchmarkRunner.Run<RecoveryLogIndexBenchmarks>(config: configuration, args: args.Skip(2).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != (writeOnly ? 1 : 5))
                throw new InvalidOperationException("All requested recovery-log measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--market-data-service-context")
        {
            await new MarketDataServiceContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new MarketDataServiceContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<MarketDataServiceContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 8)
                throw new InvalidOperationException("All eight Market Data Service-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--portfolio-context")
        {
            await new PortfolioContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new PortfolioContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<PortfolioContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 6)
                throw new InvalidOperationException("All six Portfolio-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-trade-plan-chart-buckets")
        {
            await TradePlanChartBucketBenchmarks.PrepareAsync(verifyApplied: true);
        }
        else if (args.FirstOrDefault() == "--trade-plan-chart-buckets")
        {
            await TradePlanChartBucketBenchmarks.PrepareAsync();
            var job = Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithUnrollFactor(1);
            var configuration = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(job.WithId("ChartBucketBaseline").WithInvocationCount(4),
                    job.WithId("ChartBucketSeeks").WithInvocationCount(64));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                (benchmark.Descriptor.WorkloadMethod.Name == nameof(TradePlanChartBucketBenchmarks.Current)) ==
                (benchmark.Job.Id == "ChartBucketBaseline")));
            var summary = BenchmarkRunner.Run<TradePlanChartBucketBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four chart bucket measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-trade-plan-history-metadata")
        {
            await new TradePlanMetadataBenchmarks().Setup();
        }
        else if (args.FirstOrDefault() is "--trade-plan-history-metadata" or "--trade-plan-history-metadata-older")
        {
            var older = args[0] == "--trade-plan-history-metadata-older";
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("TradePlanHistoryMetadata").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(24).WithUnrollFactor(1));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                older ? benchmark.Descriptor.WorkloadMethod.Name.Contains("Older", StringComparison.Ordinal)
                    || benchmark.Descriptor.WorkloadMethod.Name == nameof(TradePlanMetadataBenchmarks.GuardedRecentMetadata)
                    : !benchmark.Descriptor.WorkloadMethod.Name.Contains("Older", StringComparison.Ordinal)
                    && benchmark.Descriptor.WorkloadMethod.Name != nameof(TradePlanMetadataBenchmarks.GuardedRecentMetadata)));
            var summary = BenchmarkRunner.Run<TradePlanMetadataBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != (older ? 4 : 8))
                throw new InvalidOperationException("All selected Trade Plan metadata measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--trade-plan-history-paging")
        {
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("TradePlanHistoryPaging").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(12).WithInvocationCount(32).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<TradePlanHistoryPagingBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 9)
                throw new InvalidOperationException("All nine Trade Plan paging measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--trade-plan-context")
        {
            await new TradePlanContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new TradePlanContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<TradePlanContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four Trade Plan-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--intraday-indicator-index")
        {
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("IntradayIndicatorIndex").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(12).WithInvocationCount(64).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<IntradayIndicatorIndexBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 24)
                throw new InvalidOperationException("All 24 intraday indicator measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--rsi-daily-index")
        {
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("RsiDailyIndex").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(5).WithIterationCount(20).WithInvocationCount(128).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<RsiDailyIndexBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four RSI daily index measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--market-data-context")
        {
            await new MarketDataContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new MarketDataContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<MarketDataContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four Market Data-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-fund-order-fills-migration")
        {
            await FundOrderFillsIndexBenchmarks.PrepareAsync(withIndex: true, compareSplit: true, verifyMigration: true);
        }
        else if (args.FirstOrDefault() is "--fund-order-fills-index" or "--fund-order-fills-query-split")
        {
            var compareSplit = args[0] == "--fund-order-fills-query-split";
            var withIndex = compareSplit || (args.Skip(1).FirstOrDefault() switch
            {
                "baseline" => false,
                "candidate" => true,
                _ => throw new ArgumentException("Specify baseline or candidate.")
            });
            await FundOrderFillsIndexBenchmarks.PrepareAsync(withIndex, compareSplit);
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("FundOrderFillsIndex").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(5).WithIterationCount(20).WithInvocationCount(64).WithUnrollFactor(1));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
            {
                var method = benchmark.Descriptor.WorkloadMethod.Name;
                return compareSplit
                    ? method != nameof(FundOrderFillsIndexBenchmarks.InsertOrderRevision)
                    : method is not (nameof(FundOrderFillsIndexBenchmarks.SplitFirstPage) or nameof(FundOrderFillsIndexBenchmarks.SplitDeepPage));
            }));
            var summary = BenchmarkRunner.Run<FundOrderFillsIndexBenchmarks>(config: configuration,
                args: args.Skip(compareSplit ? 1 : 2).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != (compareSplit ? 4 : 3))
                throw new InvalidOperationException("All fund order fills measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--trade-context")
        {
            await new TradeContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new TradeContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<TradeContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four Trade-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--latest-tick-id")
        {
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("LatestTickId").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(5).WithIterationCount(20).WithInvocationCount(128).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<LatestTickIdBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four latest tick ID measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() is "--trade-tick-quote-context" or "--trade-tick-quote-context-long")
        {
            await new TradeTickQuoteContextConventionBenchmarks { RowCount = 64 }.Setup();
            await new TradeTickQuoteContextConventionBenchmarks { RowCount = 512 }.Setup();
            var extended = args[0] == "--trade-tick-quote-context-long";
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(extended ? 5 : 3).WithIterationCount(extended ? 12 : 8)
                .WithInvocationCount(extended ? 96 : 16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<TradeTickQuoteContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four raw Scylla-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-duplicate-index-removal")
        {
            await DuplicateIndexBenchmarks.PrepareAsync(remove: true, verifyMigration: true);
        }
        else if (args.FirstOrDefault() is "--duplicate-index-removal" or "--duplicate-index-removal-country-check")
        {
            var countryOnly = args[0] == "--duplicate-index-removal-country-check";
            var remove = args.Skip(1).FirstOrDefault() switch
            {
                "baseline" => false,
                "candidate" => true,
                _ => throw new ArgumentException("Specify baseline or candidate.")
            };
            await DuplicateIndexBenchmarks.PrepareAsync(remove);
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("DuplicateIndexRemoval").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(48).WithUnrollFactor(1));
            if (countryOnly)
                configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                    benchmark.Parameters.Items.Any(parameter => Equals(parameter.Value, "CountryCode"))));
            var summary = BenchmarkRunner.Run<DuplicateIndexBenchmarks>(config: configuration, args: args.Skip(2).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != (countryOnly ? 2 : 18))
                throw new InvalidOperationException("All requested duplicate index measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-portfolio-available-cash")
        {
            await AvailableCashBenchmarks.PrepareAsync(verifyApplied: true);
        }
        else if (args.FirstOrDefault() == "--portfolio-available-cash")
        {
            await AvailableCashBenchmarks.PrepareAsync();
            var job = Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithUnrollFactor(1);
            var configuration = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(job.WithId("AvailableCash").WithInvocationCount(32),
                    job.WithId("AvailableCashDenseBaseline").WithInvocationCount(1));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
            {
                var denseBaseline = benchmark.Descriptor.WorkloadMethod.Name == nameof(AvailableCashBenchmarks.Current)
                    && benchmark.Parameters.Items.Any(parameter => Equals(parameter.Value, 2));
                return denseBaseline == (benchmark.Job.Id == "AvailableCashDenseBaseline");
            }));
            var summary = BenchmarkRunner.Run<AvailableCashBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 8)
                throw new InvalidOperationException("All eight AvailableCash measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--measure-option-chain-quote-rewrite")
        {
            await OptionChainStatisticsWriteBenchmarks.MeasureRewriteAsync();
        }
        else if (args.FirstOrDefault() == "--verify-option-chain-quote-vacuum-cycles")
        {
            var cycles = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 6;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cycles);
            bool? normalRewrite = args.Skip(3).FirstOrDefault() switch
            {
                null => null,
                "normal-packed" => false,
                "normal-rewritten" => true,
                _ => throw new ArgumentException("Specify normal-packed or normal-rewritten.")
            };
            var intervalSeconds = args.Length > 4 ? int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 0;
            ArgumentOutOfRangeException.ThrowIfNegative(intervalSeconds);
            if (intervalSeconds > 0 && !normalRewrite.HasValue)
                throw new ArgumentException("Pacing requires a normal maintenance arm.");
            await OptionChainStatisticsWriteBenchmarks.VerifyVacuumCyclesAsync(
                rotating: args.Skip(1).FirstOrDefault() == "rotating", cycles: cycles, normalRewrite: normalRewrite,
                cycleInterval: TimeSpan.FromSeconds(intervalSeconds));
        }
        else if (args.FirstOrDefault() == "--verify-option-chain-quote-fillfactor")
        {
            await OptionChainStatisticsWriteBenchmarks.VerifyFillfactorMigrationAsync();
        }
        else if (args.FirstOrDefault() == "--option-chain-quote-fillfactor")
        {
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("OptionChainQuoteFillfactor").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(48).WithUnrollFactor(1));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                benchmark.Parameters.Items.Any(parameter =>
                    parameter.Name == nameof(OptionChainQuoteFillfactorBenchmarks.WithIndexes) && Equals(parameter.Value, true))
                && benchmark.Parameters.Items.Any(parameter => parameter.Value is OptionQuoteWrite.QuoteUpdate or OptionQuoteWrite.StatisticsUpdate)));
            var summary = BenchmarkRunner.Run<OptionChainQuoteFillfactorBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four fillfactor measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--option-chain-statistics-writes")
        {
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("OptionChainStatisticsWrites").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(48).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<OptionChainStatisticsWriteBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 8)
                throw new InvalidOperationException("All eight quote write measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-option-chain-statistics-migration")
        {
            await OptionChainStatisticsBenchmarks.PrepareAsync(withIndexes: true, verifyMigration: true);
        }
        else if (args.FirstOrDefault() == "--option-chain-statistics-discovery")
        {
            await OptionChainStatisticsBenchmarks.PrepareAsync(withIndexes: true);
            await OptionChainStatisticsBenchmarks.VerifyContractDiscoveryAsync();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("OptionChainStatisticsDiscovery").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(16).WithUnrollFactor(1));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                benchmark.Descriptor.WorkloadMethod.Name != nameof(OptionChainStatisticsBenchmarks.Baseline)));
            var summary = BenchmarkRunner.Run<OptionChainStatisticsBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 6)
                throw new InvalidOperationException("All six contract discovery measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-option-chain-statistics-ordered-scans")
        {
            await OptionChainStatisticsBenchmarks.PrepareAsync(withIndexes: true);
            await OptionChainStatisticsBenchmarks.VerifyContractDiscoveryAsync(verifyApplied: true);
        }
        else if (args.FirstOrDefault() == "--option-chain-statistics-seeks")
        {
            var indexed = args.Skip(1).FirstOrDefault() switch
            {
                "baseline" => false,
                "candidate" => true,
                _ => throw new ArgumentException("Specify baseline or candidate.")
            };
            await OptionChainStatisticsBenchmarks.PrepareAsync(indexed);
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("OptionChainStatisticsSeeks").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(16).WithUnrollFactor(1));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                benchmark.Descriptor.WorkloadMethod.Name == nameof(OptionChainStatisticsBenchmarks.Baseline)
                || (indexed && benchmark.Descriptor.WorkloadMethod.Name == nameof(OptionChainStatisticsBenchmarks.Seeks))));
            var summary = BenchmarkRunner.Run<OptionChainStatisticsBenchmarks>(config: configuration, args: args.Skip(2).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != (indexed ? 4 : 2))
                throw new InvalidOperationException("All selected option chain statistics measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-pending-business-handoff-index")
        {
            await PendingBusinessHandoffBenchmarks.VerifyMigrationAsync(earlyBacklog: args.Skip(1).FirstOrDefault() == "early");
        }
        else if (args.FirstOrDefault() == "--pending-business-handoffs")
        {
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("PendingBusinessHandoffs").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(24).WithUnrollFactor(1));
            configuration.AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(benchmark =>
                benchmark.Parameters.Items.Any(parameter => Equals(parameter.Value, HandoffWorkload.Read)) ||
                benchmark.Parameters.Items.Any(parameter =>
                    parameter.Name == nameof(PendingBusinessHandoffBenchmarks.EarlyBacklog) && Equals(parameter.Value, false))));
            var summary = BenchmarkRunner.Run<PendingBusinessHandoffBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 8)
                throw new InvalidOperationException("All eight business handoff measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-receipt-anti-joins")
        {
            await ReceiptAntiJoinBenchmarks.PrepareAsync(earlyPending: args.Skip(1).FirstOrDefault() == "early", verifyApplied: true);
        }
        else if (args.FirstOrDefault() is "--receipt-anti-joins" or "--receipt-anti-joins-early-backlog")
        {
            await ReceiptAntiJoinBenchmarks.PrepareAsync(earlyPending: args[0] == "--receipt-anti-joins-early-backlog");
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ReceiptAntiJoins").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(4).WithIterationCount(14).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<ReceiptAntiJoinBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 9)
                throw new InvalidOperationException("All nine receipt anti-join measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--verify-projector-outbox-index-migration")
        {
            await ProjectorOutboxIndexBenchmarks.PrepareAsync(withIndexes: true, verifyMigration: true);
        }
        else if (args.FirstOrDefault() is "--projector-outbox-index" or "--projector-outbox-claim-order")
        {
            var compareOrder = args[0] == "--projector-outbox-claim-order";
            var withIndexes = args.Skip(1).FirstOrDefault() switch
            {
                "baseline" => false,
                "candidate" => true,
                _ => throw new ArgumentException("Specify baseline or candidate.")
            };
            ProjectorOutboxIndexBenchmarks.UseOrderedClaim = compareOrder && withIndexes;
            await ProjectorOutboxIndexBenchmarks.PrepareAsync(compareOrder || withIndexes, compareOrder: compareOrder);
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ProjectorOutboxIndex").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(5).WithIterationCount(20).WithInvocationCount(32).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<ProjectorOutboxIndexBenchmarks>(config: configuration, args: args.Skip(2).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 2)
                throw new InvalidOperationException("Both projector outbox measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--event-source-context")
        {
            await new EventSourceContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new EventSourceContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<EventSourceContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four EventSource-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--reference-context")
        {
            await new ReferenceContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new ReferenceContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<ReferenceContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four Reference-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--configuration-context")
        {
            await new ConfigurationContextConventionBenchmarks { ColdConnection = false }.Setup();
            await new ConfigurationContextConventionBenchmarks { ColdConnection = true }.Setup();
            var configuration = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Default
                .WithId("ContextQualification").WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1));
            var summary = BenchmarkRunner.Run<ConfigurationContextConventionBenchmarks>(config: configuration, args: args.Skip(1).ToArray());
            if (summary.Reports.Count(report => report.ResultStatistics is not null) != 4)
                throw new InvalidOperationException("All four Configuration-context measurements must complete successfully.");
        }
        else if (args.FirstOrDefault() == "--plan-business-load")
            await PostgresPlanBusinessLoadRunner.RunAsync(args.Skip(1).ToArray());
        else if (args.FirstOrDefault() == "--plan-business")
            BenchmarkRunner.Run<PostgresPlanBusinessBenchmarks>(config: ManualConfig.Create(DefaultConfig.Instance).WithBuildTimeout(TimeSpan.FromMinutes(10)), args: args.Skip(1).ToArray());
        else
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
