using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.PortfolioLegacyImport.Console;

internal static class Program
{
    const string PortfolioName = "Legacy Test Portfolio";

    public static async Task<int> Main(string[] args)
    {
        var execute = args.Any(x => x.Equals("--execute", StringComparison.OrdinalIgnoreCase));
        var natsUrl = Value(args, "--nats-url") ?? Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await using var connections = new NatsConnectionManager();
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = natsUrl }, NullLogger.Instance, connections);
        try
        {
            await producer.StartAsync(new ActorMailboxId(ActorType.Query, "PortfolioLegacyImport"), cancellation.Token);
            var queries = new PortfolioQueryApi(producer);
            var catalogResult = await queries.GetLegacyFundCatalogAsync(cancellation.Token);
            var catalog = Require(catalogResult).Where(x => !x.IsUnassigned && x.Fund.FundId > 0).OrderBy(x => x.Fund.FundId).ToArray();
            var unassigned = Require(catalogResult).Where(x => x.IsUnassigned).OrderBy(x => x.Fund.FundId).ToArray();
            await global::System.Console.Out.WriteLineAsync($"Legacy source: {catalog.Length} valid funds; {catalog.Sum(x => x.OrderCount)} orders; {catalog.Sum(x => x.CompositionTradeCount)} composition trades.");
            if (unassigned.Length > 0)
                await global::System.Console.Out.WriteLineAsync($"Quarantine: {unassigned.Length} orphan FundIds; {unassigned.Sum(x => x.OrderCount)} orders; {unassigned.Sum(x => x.CompositionTradeCount)} composition trades (still queryable, never mapped as canonical funds).");
            if (!execute)
            {
                await global::System.Console.Out.WriteLineAsync("Plan only. Re-run with --execute to create/resume the Draft Portfolio import.");
                return 0;
            }

            var identity = new PortfolioIdentityApi(producer);
            var portfolioCommands = new PortfolioCommandApi(producer);
            var fundCommands = new PortfolioFundCommandApi(producer, queries);
            var scopes = Require(await queries.GetLegacyPortfolioScopesAsync(cancellation.Token));
            var scope = scopes.SingleOrDefault(x => x.Portfolio.Name.Equals(PortfolioName, StringComparison.Ordinal));
            PortfolioReadModel portfolio;
            if (scope is null)
            {
                var draftPage = Require(await queries.GetPortfoliosAsync(PortfolioOperatingState.Draft, 200, cancellationToken: cancellation.Token));
                portfolio = draftPage.Items.SingleOrDefault(x => x.Name.Equals(PortfolioName, StringComparison.Ordinal))
                    ?? await CreatePortfolioAsync(identity, portfolioCommands, queries, cancellation.Token);
            }
            else portfolio = scope.Portfolio;

            var existing = scope?.Funds.Where(x => x.HistoricalSourceFundId.HasValue)
                .ToDictionary(x => x.HistoricalSourceFundId!.Value) ?? [];
            foreach (var source in catalog)
            {
                if (existing.TryGetValue(source.Fund.FundId, out var mapped))
                {
                    await global::System.Console.Out.WriteLineAsync($"skip source Fund {source.Fund.FundId}: mapped to Fund {mapped.FundId}");
                    continue;
                }
                var fundId = Require(await identity.AllocateFundIdAsync(cancellation.Token)).Value;
                var mandate = Mandate(portfolio.PortfolioId, fundId, source);
                RequireSuccess(await fundCommands.CreateFundMandateAsync(mandate, Guid.NewGuid(), cancellation.Token));
                await WaitForFundAsync(queries, portfolio.PortfolioId, fundId, cancellation.Token);
                var revision = Require(await queries.GetPortfolioRevisionAsync(portfolio.PortfolioId, cancellation.Token)).Revision;
                RequireSuccess(await portfolioCommands.AddFundAsync(new PortfolioFundId(portfolio.PortfolioId, fundId), revision, cancellation.Token));
                await WaitForPortfolioRevisionAsync(queries, portfolio.PortfolioId, revision + 1, cancellation.Token);
                await global::System.Console.Out.WriteLineAsync($"mapped source Fund {source.Fund.FundId} -> Portfolio Fund {fundId}");
            }

            var qualified = Require(await queries.GetLegacyPortfolioScopesAsync(cancellation.Token))
                .Single(x => x.Portfolio.PortfolioId == portfolio.PortfolioId);
            if (qualified.Funds.Length != catalog.Length || qualified.Funds.Any(x => !x.IsLegacyHistory || x.OperatingState != FundOperatingState.Draft))
                throw new InvalidOperationException("Legacy import qualification failed: mapping count/state mismatch.");
            await global::System.Console.Out.WriteLineAsync($"QUALIFIED Portfolio {portfolio.PortfolioId} '{portfolio.Name}': {qualified.Funds.Length} read-only Draft fund mappings.");
            await VerifyHistoryAsync(queries, Require(catalogResult), cancellation.Token);
            return 0;
        }
        catch (Exception exception)
        {
            await global::System.Console.Error.WriteLineAsync(exception.ToString());
            return 1;
        }
        finally
        {
            await producer.StopAsync();
        }
    }

    static async Task<PortfolioReadModel> CreatePortfolioAsync(IPortfolioIdentityApi identity, IPortfolioCommandApi commands, IPortfolioQueryApi queries, CancellationToken cancellationToken)
    {
        var id = Require(await identity.AllocatePortfolioIdAsync(cancellationToken)).Value;
        var now = DateTime.UtcNow;
        var portfolio = new PortfolioReadModel
        {
            PortfolioId = id, PortfolioVersion = 1, Name = PortfolioName, BaseCurrency = "USD",
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
            CreatedOnUtc = now, CreatedBy = "legacy-portfolio-import",
        };
        RequireSuccess(await commands.CreatePortfolioAsync(portfolio, Guid.NewGuid(), cancellationToken));
        await WaitForPortfolioRevisionAsync(queries, id, 1, cancellationToken);
        await global::System.Console.Out.WriteLineAsync($"created Draft Portfolio {id} '{PortfolioName}'");
        return portfolio;
    }

    static FundMandateReadModel Mandate(int portfolioId, int fundId, LegacyFundHistoryReadModel source)
    {
        var now = DateTime.UtcNow;
        return new FundMandateReadModel
        {
            PortfolioId = portfolioId, FundId = fundId, FundCode = $"LEGACY-{source.Fund.FundId}", Name = source.Fund.Name,
            FundMandateVersion = 1, TradingYear = now.Year, OperatingState = FundOperatingState.Draft,
            EffectiveFromUtc = now, DecisionHorizon = "LegacyHistory",
            Objective = string.IsNullOrWhiteSpace(source.Fund.Description) ? "Read-only imported Fund history" : source.Fund.Description,
            UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Options"], PermittedDirections = [], PermittedConditions = [],
            PermittedTradeFamilies = ["IRON_CONDOR", "VERTICAL_SPREAD"], CreatedOnUtc = now, CreatedBy = "legacy-portfolio-import",
            HistoricalSource = "FundLegacyDb", HistoricalSourceFundId = source.Fund.FundId,
        };
    }

    static async Task WaitForFundAsync(IPortfolioQueryApi queries, int portfolioId, int fundId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var result = await queries.GetFundAsync(portfolioId, fundId, cancellationToken: cancellationToken);
            if (result.Success) return;
            await Task.Delay(100, cancellationToken);
        }
        throw new TimeoutException($"Fund {fundId} projection did not appear.");
    }

    static async Task WaitForPortfolioRevisionAsync(IPortfolioQueryApi queries, int portfolioId, long expected, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var result = await queries.GetPortfolioRevisionAsync(portfolioId, cancellationToken);
            if (result.Success && result.Value?.Revision >= expected) return;
            await Task.Delay(100, cancellationToken);
        }
        throw new TimeoutException($"Portfolio {portfolioId} projection did not reach revision {expected}.");
    }

    static async Task VerifyHistoryAsync(IPortfolioQueryApi queries, LegacyFundHistoryReadModel[] catalog, CancellationToken cancellationToken)
    {
        var orderCount = 0;
        var compositionCount = 0;
        var statusExamples = new Dictionary<LegacyTradeMatchStatus, string>();
        foreach (var fund in catalog.OrderBy(x => x.Fund.FundId))
        {
            var orders = Require(await queries.GetLegacyFundOrdersAsync(fund.Fund.FundId,
                new DateOnly(1900, 1, 1), new DateOnly(2100, 12, 31), 1000, cancellationToken));
            if (orders.Length != fund.OrderCount || orders.Sum(x => x.CompositionTradeCount) != fund.CompositionTradeCount)
                throw new InvalidOperationException($"History qualification mismatch for source Fund {fund.Fund.FundId}.");
            orderCount += orders.Length;
            compositionCount += orders.Sum(x => x.CompositionTradeCount);
            foreach (var order in orders.Where(x => x.CompositionTradeCount > 0).Take(30))
            {
                var trades = Require(await queries.GetLegacyFundOrderTradesAsync(fund.Fund.FundId, order.Order.OrderId, cancellationToken));
                foreach (var trade in trades)
                    statusExamples.TryAdd(trade.MatchStatus, $"{trade.Composition.FundId}:{trade.Composition.OrderId}:{trade.Composition.TradeId}");
                if (statusExamples.Count == Enum.GetValues<LegacyTradeMatchStatus>().Length) break;
            }
        }
        if (orderCount != catalog.Sum(x => x.OrderCount) || compositionCount != catalog.Sum(x => x.CompositionTradeCount))
            throw new InvalidOperationException("History qualification aggregate mismatch.");
        var assigned = catalog.Where(x => !x.IsUnassigned).ToArray();
        var quarantined = catalog.Where(x => x.IsUnassigned).ToArray();
        await global::System.Console.Out.WriteLineAsync($"QUALIFIED NATS history: {orderCount} FundOrders; {compositionCount} FundOrderTrades; all assigned and quarantined source rows queryable.");
        await global::System.Console.Out.WriteLineAsync($"  assigned={assigned.Sum(x => x.OrderCount)} orders/{assigned.Sum(x => x.CompositionTradeCount)} trades; quarantined={quarantined.Sum(x => x.OrderCount)} orders/{quarantined.Sum(x => x.CompositionTradeCount)} trades.");
        await global::System.Console.Out.WriteLineAsync("TradeDb evidence samples: " + string.Join(", ", statusExamples.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}")));
    }

    static T Require<T>(ServiceResult<T> result) where T : class =>
        result.Success && result.Value is not null ? result.Value : throw new InvalidOperationException($"{result.ErrorCode}: {result.ErrorMessage}");

    static void RequireSuccess(ServiceResult<Guid> result)
    {
        if (!result.Success) throw new InvalidOperationException($"{result.ErrorCode}: {result.ErrorMessage}");
    }

    static string? Value(string[] args, string name)
    {
        var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
