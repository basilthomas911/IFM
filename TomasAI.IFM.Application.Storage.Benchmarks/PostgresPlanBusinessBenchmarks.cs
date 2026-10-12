
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Extensions;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Real atomic plan writes alongside actual ledger posting transactions in an isolated database.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 8)]
public class PostgresPlanBusinessBenchmarks
{
    public string Connection => Environment.GetEnvironmentVariable("IFM_PLAN_BENCHMARK_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=ifm_storage_refactor_test";
    public IEnumerable<string> Profiles => [Environment.GetEnvironmentVariable("IFM_PLAN_BENCHMARK_PROFILE") ?? "AtomicDurable"];
    [ParamsSource(nameof(Profiles))]
    public string Profile { get; set; } = Environment.GetEnvironmentVariable("IFM_PLAN_BENCHMARK_PROFILE") ?? "AtomicDurable";
    PostgresEventTransaction transactionOwner = null!;
    PostgresEventTransaction projectionOwner = null!;
    bool Split => Profile.StartsWith("Split", StringComparison.Ordinal);
    string ProjectionConnection => Profile == "SplitSeparateUnsafeProjection"
        ? Environment.GetEnvironmentVariable("IFM_PLAN_BENCHMARK_PROJECTION_CONNECTION")
            ?? throw new InvalidOperationException("A separate isolated projection connection is required.")
        : Connection;
    TradePlanDbContext plans = null!;
    FinancialBookConfiguration book = null!;
    long planRevision, financialRevision, sourceOnlyRevision, projectionOnlyRevision;
    int planIdentity;
    string stream = "";
    PostgresEventTransaction Transactions() => transactionOwner;

    [GlobalSetup]
    public async Task Setup()
    {
        if (Profile is not ("AtomicDurable" or "AtomicAsyncCommit" or "AtomicNoFsync" or "AtomicNoFsyncAsyncCommit" or "SplitSameInstanceAsyncProjection" or "SplitSeparateUnsafeProjection"))
            throw new InvalidOperationException("Unknown durability benchmark profile.");
        if (Profile != "AtomicDurable" && Environment.GetEnvironmentVariable("IFM_PLAN_BENCHMARK_CONNECTION") is null)
            throw new InvalidOperationException("Nondefault profiles require the isolated durability benchmark harness.");
        transactionOwner = new(new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, Connection, "System.Data.Postgres"));
        projectionOwner = new(new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, ProjectionConnection, "System.Data.Postgres"));
        if (Environment.GetEnvironmentVariable("IFM_PLAN_BENCHMARK_CONNECTION") is not null)
        {
            await VerifySettings(Connection, Profile is "AtomicNoFsync" or "AtomicNoFsyncAsyncCommit" ? "off" : "on",
                Profile is "AtomicAsyncCommit" or "AtomicNoFsyncAsyncCommit" ? "off" : "on");
            if (Profile == "SplitSeparateUnsafeProjection") await VerifySettings(ProjectionConnection, "off", "off");
        }
        var settings = new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, Connection, "System.Data.Postgres")
            .Add(TradePlanDbContext.TradePlanDbConnection, ProjectionConnection, "System.Data.Postgres");
        await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new TradePlanSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        plans = new(settings, NullLogger<DbProvider>.Instance);
        book = await CreateBook();
        planIdentity = Random.Shared.Next(10_000_000, 100_000_000);
        stream = "benchmark-plan-" + Guid.NewGuid().ToString("N");
    }

    [Benchmark]
    public async Task PlanWrite()
    {
        var revision = planRevision + 1;
        var plan = BuildPlan(revision);
        var source = BuildSource(plan);
        await Transactions().ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync(stream, source.CommandId, source, planRevision, token);
            if (!Split) await plans.ProjectMaterialAsync(transaction, plan, token);
            return true;
        });
        // In split profiles the source commit has already succeeded: projection failure cannot roll it back.
        planRevision = revision;
        if (Split)
            await projectionOwner.ExecuteOnceAsync(async (transaction, token) =>
            {
                await transaction.ExecuteAsync("SET LOCAL synchronous_commit = off", [], token);
                await plans.ProjectMaterialAsync(transaction, plan, token);
                return true;
            });
    }

    [Benchmark]
    public async Task EventAppendAndCommit()
    {
        var source = BuildSource(BuildPlan(sourceOnlyRevision + 1));
        await Transactions().ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync(stream + "-source-component", source.CommandId, source, sourceOnlyRevision, token);
            return true;
        });
        sourceOnlyRevision++;
    }

    [Benchmark]
    public async Task ProjectionWriteAndCommit()
    {
        var plan = BuildPlan(projectionOnlyRevision + 1);
        await projectionOwner.ExecuteOnceAsync(async (transaction, token) =>
        {
            if (Split) await transaction.ExecuteAsync("SET LOCAL synchronous_commit = off", [], token);
            await plans.ProjectMaterialAsync(transaction, plan, token);
            return true;
        });
        projectionOnlyRevision++;
    }

    StrategyTradePlanSnapshot BuildPlan(long revision)
    {
        var plan = Plan(planIdentity, revision, -42m, true);
        plan = plan with { IronCondorTradePlanSnapshot = new() { Position = plan.Position, SourceEventId = Guid.NewGuid(), SequenceId = revision, DailyPnl = -42m, ForwardLossRatio = 0.8, ValueDate = plan.ValueDate } };
        return plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
    }

    static IronCondorTradePlanUpdatedEvent BuildSource(StrategyTradePlanSnapshot plan) => new()
    {
        Id = plan.IronCondorTradePlanSnapshot!.SourceEventId, CommandId = Guid.NewGuid(), Plan = plan, ReceivedOn = DateTime.UtcNow
    };

    [Benchmark]
    public async Task BusinessLedgerPosting()
    {
        var result = await Post(Request(book, LedgerTransactionKind.DepositConfirmed, 1m, financialRevision));
        financialRevision = result.Receipt.FinancialRevision;
    }

    /// <summary>Reads the same position's latest plan and one value-date history page while snapshots are being written.</summary>
    public async Task ReadPlanLatestAndHistory()
    {
        var key = Plan(planIdentity, Math.Max(1, planRevision), -42m, true);
        _ = await plans.GetCurrentAsync(key.Position.Id, TradeStrategyKind.IronCondor, key.ValueDate);
        _ = await plans.GetHistoryAsync(key.Position.Id, TradeStrategyKind.IronCondor, key.ValueDate, 64, null);
    }

    [Benchmark]
    public Task ConcurrentPlanAndBusiness() => Task.WhenAll(PlanWrite(), BusinessLedgerPosting());

    static async Task VerifySettings(string connectionString, string fsync, string sync)
    {
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        if (settings.Database != "ifm_durability_benchmark" || settings.Host != "127.0.0.1" || settings.Port is < 56540 or > 56541)
            throw new InvalidOperationException("Durability tests require dedicated localhost benchmark instances.");
        await using var connection = new TomasAI.IFM.Framework.Storage.Postgres.PostgresObjectDataRepositoryConnection().As<Npgsql.NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("SELECT current_setting('fsync'), current_setting('synchronous_commit'), current_setting('full_page_writes')", connection);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        if (reader.GetString(0) != fsync || reader.GetString(1) != sync || reader.GetString(2) != "on")
            throw new InvalidOperationException("Effective server settings differ from the benchmark profile.");
    }

    async Task<FinancialBookConfiguration> CreateBook(Func<FinancialBookConfiguration, FinancialBookConfiguration>? configure = null)
    {
        var transactions = Transactions(); await new PortfolioFinancialSchema(transactions).InitializeAsync();
        var id = Random.Shared.Next(100000, 1000000000);
        var fund = new FinancialFundAuthority
        {
            FundId = id + 1,
            CanSpend = true,
            PortfolioStreamVersion = 1,
            FundStreamVersion = 1,
            PolicyStreamVersion = 1,
            Reference = new() { PolicyId = id + 2, ValidUntilUtc = DateTime.UtcNow.AddDays(1) }
        };
        var book = new FinancialBookConfiguration { BookId = id, PortfolioId = id, AccountingEntityId = Guid.NewGuid(), ExecutionAccountReference = Guid.NewGuid().ToString("N"), Environment = "DevelopmentBenchmark", Funds = [fund], MigrationQualified = true };
        if (configure is not null) book = configure(book);
        await transactions.ExecuteAsync(async (db, ct) =>
        {
            foreach (var stream in new[] { $"Portfolio.{id}", $"PortfolioFinancialPolicy.{id}.{id + 2}" }.Concat(book.Funds.Select(x => $"PortfolioFund.{id}.{x.FundId}")))
            {
                var command = Guid.NewGuid();
                await db.AppendAsync(stream, command, new PortfolioCreatedEvent(Guid.NewGuid(), command, 1, DateTime.UtcNow, "benchmark", new()), 0, ct);
            }
            return true;
        });
        await new PortfolioFinancialStore(transactions).CreateBookAsync(book,
            [new(101, 1, "Cash", PostingSide.Debit, true, "cash"), new(102, 1, "Equity", PostingSide.Credit, true, "equity"), new(103, 1, "Expense", PostingSide.Debit, true, "expense")],
            [Rule(LedgerTransactionKind.DepositConfirmed), Rule(LedgerTransactionKind.WithdrawalRequested), Rule(LedgerTransactionKind.Commission)],
            new DateOnly(2020, 1, 1), new DateOnly(2099, 12, 31));
        return book;
    }
    static LedgerPostingRule Rule(LedgerTransactionKind kind) => new(new Guid((int)kind, 0, 0, new byte[8]), 1, $"rule-{kind}", kind,
        new(kind == LedgerTransactionKind.Commission ? 103 : 101, 1), new(kind == LedgerTransactionKind.Commission ? 101 : 102, 1), kind != LedgerTransactionKind.WithdrawalRequested);
    internal static PostFundTransactionCommand Request(FinancialBookConfiguration book, LedgerTransactionKind kind, decimal amount, long revision)
    {
        var rule = Rule(kind); var operation = Guid.NewGuid(); var now = DateTime.UtcNow;
        var request = new PostFundTransactionCommand
        {
            CommandId = operation,
            OperationId = operation,
            PortfolioId = book.PortfolioId,
            EntityId = new(book.PortfolioId),
            Subject = new(ActorType.Command, PostFundTransactionCommand.Actor, PostFundTransactionCommand.Verb, book.PortfolioId.ToString()),
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            RequestedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(2),
            ExpectedFinancialRevision = revision,
            Access = new("benchmark", ["PortfolioAdministrator"]),
            Body = new()
            {
                BookId = book.BookId,
                FundId = book.Funds[0].FundId,
                Currency = "USD",
                Amount = amount,
                TransactionKind = kind,
                AccountingDate = DateOnly.FromDateTime(now),
                ValueDate = DateOnly.FromDateTime(now),
                Source = new() { System = "DevelopmentBenchmark", SourceEventId = Guid.NewGuid(), SourceContentHash = Guid.NewGuid().ToString("N"), OccurredAtUtc = now },
                PostingRule = new() { RuleId = rule.RuleId, Version = rule.Version, ContentHash = rule.ContentHash },
                MovementEvidence = new() { Status = MovementStatus.Confirmed, SourceReference = "integration-movement" }
            }
        };
        return request with { InputSha256 = FinancialCanonicalHash.Request(request) };
    }
    static PreparedLedgerPosting[] Prepare(PostFundTransactionCommand request) => [new(Random.Shared.NextInt64(100000, long.MaxValue), Random.Shared.NextInt64(100000, long.MaxValue), request.Body)];
    Task<LedgerPostingCompletedEvent> Post(PostFundTransactionCommand request) => new GeneralLedgerStore(Transactions()).PostAsync(request, Prepare(request),
        (item, rule, prior, original, remaining) => LedgerPostingModel.Calculate(item, rule, prior, original, remaining), info => Completed(request, info));
    static LedgerPostingCompletedEvent Completed(PostFundTransactionCommand request, LedgerCommitInfo info) => new()
    {
        Id = info.EventId,
        OperationId = request.OperationId,
        CommandId = request.CommandId,
        PortfolioId = request.PortfolioId,
        EntityId = request.EntityId,
        Subject = new(ActorType.Event, PostFundTransactionCommand.Actor, nameof(LedgerPostingCompletedEvent), request.EntityId.Format()),
        CommittedAtUtc = info.CommittedAtUtc,
        ReceivedOn = info.CommittedAtUtc,
        InputHash = request.InputSha256,
        CorrelationId = request.CorrelationId,
        CausationId = request.CausationId,
        Receipt = new()
        {
            OperationId = request.OperationId,
            PortfolioId = request.PortfolioId,
            BookId = request.Body.BookId,
            FundId = request.Body.FundId,
            TransactionId = info.Items[0].TransactionId,
            JournalId = info.Items[0].JournalId,
            JournalHash = info.Items[0].JournalHash,
            InputHash = request.InputSha256,
            FinancialRevision = info.Revision,
            CommittedAtUtc = info.CommittedAtUtc,
            CompletedEventId = info.EventId,
            Source = request.Body.Source,
            ObligationId = info.Items[0].ObligationId
        }
    };

    static StrategyTradePlanSnapshot Plan(int suffix, long revision, decimal pnl, bool material)
    {
        var now = DateTime.UtcNow;
        var position = new StrategyPositionSnapshot
        {
            Id = new(new TradeEntityId(suffix, suffix + 1, suffix + 2, suffix + 3),
                TradePlanContractIdentity.DeterministicId($"position-{suffix}")),
            StrategyKind = TradeStrategyKind.IronCondor,
            Phase = StrategyPositionPhase.MarkToMarket,
            PositionSequence = revision,
            RouteGeneration = 1,
            Legs = Enumerable.Range(1, 4).Select(index => new StrategyPositionLeg
            {
                TradeLegId = TradePlanContractIdentity.DeterministicId($"leg-{suffix}-{index}"),
                ContractId = $"ES-{suffix}-{index}",
                AssetFamily = TradeAssetFamily.FuturesOption,
                SignedQuantity = index % 2 == 0 ? -1 : 1,
                OpeningPrice = 10m + index,
                CurrentPrice = 10.1m + index,
                LastSourceSequence = revision,
                LastPriceAtUtc = now
            }).ToArray(),
            UnrealizedPnl = pnl,
            AsOfUtc = now,
            IsOpen = true
        };
        var plan = new StrategyTradePlanSnapshot
        {
            Position = position,
            ValueDate = DateOnly.FromDateTime(now),
            PlanRevision = revision,
            CurrentValue = 1m,
            TotalPnl = pnl,
            ForwardTradePrice = 1m,
            ForwardPnl = pnl,
            State = TradePlanState.Normal,
            Action = TradePlanAction.Monitor,
            MaterialChange = material,
            ReasonCode = "PLAN.NORMAL",
            Explanation = "isolated benchmark",
            Parameters = new(),
            CalculatedAtUtc = now
        };
        return plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
    }
}
