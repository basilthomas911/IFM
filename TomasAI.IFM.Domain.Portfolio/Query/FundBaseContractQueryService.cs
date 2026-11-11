using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Reads projected Fund rules and published Databento definitions. Never reads command state or downloads provider data.</summary>
public sealed class FundBaseContractQueryService(IPortfolioDbReadContext funds, ISecuritiesDbReadContext securities,
    IReferenceDbReadContext definitions, IValueDateProvider? valueDates)
{
    /// <summary>Resolves saved symbols against one immutable Databento generation at the server's current value date.</summary>
    public async Task<ServiceResult<FundBaseContractsReadModel>> GetAsync(int portfolioId, int fundId, long? version = null,
        CancellationToken cancellationToken = default)
    {
        var fund = await funds.GetFundAsync(fundId, cancellationToken).ConfigureAwait(false);
        if (fund is null || fund.PortfolioId != portfolioId || version is { } requested && fund.FundMandateVersion != requested)
            return new ServiceFailed<FundBaseContractsReadModel>(34100, "Fund mandate/version was not found in the Portfolio.");
        if (valueDates is null) return new ServiceFailed<FundBaseContractsReadModel>(503, "Server value date provider is unavailable.");
        var day = valueDates.ValueDate;
        var snapshot = await definitions.GetInstrumentDefinitionSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var result = new FundBaseContractsReadModel { PortfolioId = portfolioId, FundId = fundId,
            FundMandateVersion = fund.FundMandateVersion, ValueDate = day, Defaults = fund.DefensiveCopy().BaseContracts };
        if (snapshot is null || !await definitions.IsInstrumentDefinitionSelectionCompleteAsync(snapshot.Id, cancellationToken).ConfigureAwait(false))
            return new ServiceOk<FundBaseContractsReadModel>(result with { Status = "Published Databento definitions are unavailable. Refresh instrument definitions before configuring contracts." });
        var products = await definitions.GetInstrumentDefinitionSymbolsAsync(snapshot.Id, TradeStrategyFamilyType.Futures, cancellationToken).ConfigureAwait(false);
        List<FuturesContractV3ReadModel> contracts = [];
        List<string> warnings = [];
        foreach (var rule in fund.BaseContracts)
        {
            if (rule.BaseAsset != "Futures") continue;
            var catalog = await securities.GetFuturesContractsBySymbolAsync(rule.Symbol, cancellationToken).ConfigureAwait(false);
            List<InstrumentDefinitionSelection> provider = [];
            foreach (var dataset in snapshot.Datasets)
            {
                var after = string.Empty;
                for (var page = 0; page < 100; page++)
                {
                    var rows = await definitions.GetInstrumentDefinitionSelectionAsync(snapshot.Id, dataset, rule.Symbol,
                        false, after, 200, cancellationToken).ConfigureAwait(false);
                    provider.AddRange(rows.Select(x => x.Value));
                    if (rows.Count < 200) break;
                    var next = rows[^1].Key;
                    if (next == after || page == 99) throw new InvalidDataException("Unbounded Databento contract selection.");
                    after = next;
                }
            }
            var resolved = Resolve(rule, catalog, provider, day);
            var product = products.FirstOrDefault(x => x.Symbol == rule.Symbol && resolved.Any(c => c.Exchange == x.Exchange));
            var descriptions = resolved.Length == 0 ? null : await definitions.GetContractDescriptionsAsync(resolved[0].Exchange, rule.Symbol, cancellationToken).ConfigureAwait(false);
            contracts.AddRange(resolved.Select(contract => {
                if (descriptions?.TryGetValue(contract.RawSymbol, out var description) is true)
                    return contract with { Description = description };
                return product is not null && product.Description != $"{rule.Symbol} futures"
                    ? contract with { Description = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{product.Description} - {contract.LastTradeDate:yyyy-MMM-dd}") }
                    : contract;
            }));
            if (resolved.Length < rule.ForwardCycles + 1)
                warnings.Add($"{rule.Symbol}: {resolved.Length} of {rule.ForwardCycles + 1} requested contracts available from Databento.");
        }
        return new ServiceOk<FundBaseContractsReadModel>(result with { DefinitionSnapshotId = snapshot.Id,
            Symbols = products.OrderBy(x => x.Symbol, StringComparer.Ordinal).ToArray(), Contracts = contracts.ToArray(),
            Status = fund.BaseContracts.Length == 0 ? "No saved defaults. Choose at least one Futures symbol." : string.Join(" ", warnings) });
    }

    /// <summary>Includes only catalog identities matched to genuine, nondeleted Databento outright definitions; skips expired cycles.</summary>
    public static FuturesContractV3ReadModel[] Resolve(FundBaseContractRule rule, IEnumerable<FuturesContractV3ReadModel> catalog,
        IEnumerable<InstrumentDefinitionSelection> definitions, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(definitions);
        if (rule.Validate().Count != 0) return [];
        var provider = definitions.Where(x => !x.Deleted && x.InstrumentClass == "F" && x.Root == rule.Symbol &&
            x.ExpirationUtc.HasValue && x.InstrumentId != 0 && !string.IsNullOrWhiteSpace(x.Dataset) &&
            !string.IsNullOrWhiteSpace(x.DefinitionDigest) && !string.IsNullOrWhiteSpace(x.RawDefinitionReference))
            .GroupBy(x => DateOnly.FromDateTime(x.ExpirationUtc!.Value.UtcDateTime))
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.DefinitionTimestampUtc).First());
        var qualified = catalog.Where(x => x.Symbol == rule.Symbol && x.LastTradeDate >= day && provider.ContainsKey(x.LastTradeDate))
            .Where(x => x.ContractId == string.Create(System.Globalization.CultureInfo.InvariantCulture,$"{rule.Symbol}{x.LastTradeDate:yyyyMMdd}") ||
                x.LocalSymbol == provider[x.LastTradeDate].RawSymbol || x.RawSymbol == provider[x.LastTradeDate].RawSymbol)
            .OrderBy(x => x.LastTradeDate).ThenBy(x => x.ContractId, StringComparer.Ordinal).DistinctBy(x => x.LastTradeDate).ToArray();
        var current = qualified.FirstOrDefault(x => x.OnTheRun && x.Rollover);
        if (current is null) return [];
        return qualified.Where(x => x.LastTradeDate >= current.LastTradeDate).Take(rule.ForwardCycles + 1)
            .Select(x => { var definition = provider[x.LastTradeDate]; return x with { Dataset = definition.Dataset,
                InstrumentId = definition.InstrumentId, PublisherId = definition.PublisherId, RawSymbol = definition.RawSymbol,
                DefinitionDigest = definition.DefinitionDigest, DefinitionTimestampUtc = definition.DefinitionTimestampUtc,
                RawDefinitionReference = definition.RawDefinitionReference }; }).ToArray();
    }
}
