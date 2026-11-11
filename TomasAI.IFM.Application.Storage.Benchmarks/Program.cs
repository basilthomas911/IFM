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
