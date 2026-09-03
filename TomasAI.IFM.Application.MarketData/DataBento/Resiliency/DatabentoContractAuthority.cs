using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public interface ICurrentFuturesContractCatalog
{
    Task<IReadOnlyList<FuturesContractV3ReadModel>> GetByRootAsync(string rootSymbol, CancellationToken cancellationToken);
}

public interface IDatabentoContractAuthority
{
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReconcileAsync(DateOnly valueDate,
        string changedBy, CancellationToken cancellationToken);
}

/// <summary>Reconciles the three required PostgreSQL roles from the read-only source catalog.</summary>
public sealed class DatabentoContractAuthority(
    IMarketDataServiceStore store,
    ICurrentFuturesContractCatalog sourceCatalog,
    IDatabentoContractRegistrationRegistry registrations,
    TimeProvider timeProvider) : IDatabentoContractAuthority
{
    public async Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReconcileAsync(
        DateOnly valueDate, string changedBy, CancellationToken cancellationToken)
    {
        if (valueDate == default) throw new ArgumentOutOfRangeException(nameof(valueDate));
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);
        var esSources = Eligible(await sourceCatalog.GetByRootAsync("ES", cancellationToken).ConfigureAwait(false), valueDate)
            .Where(value => value.LastTradeDate.Month is 3 or 6 or 9 or 12).ToArray();
        var vxSources = Eligible(await sourceCatalog.GetByRootAsync("VX", cancellationToken).ConfigureAwait(false), valueDate);
        if (esSources.Length == 0 || vxSources.Length < 2)
            throw new InvalidOperationException("The source catalog must contain one eligible ES and two ordered VX contracts.");

        var existing = (await store.ListAssignmentsAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(value => value.ContractRole);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var es = Copy(DatabentoContractRole.EsQuarterly, esSources[0], existing, now, changedBy);
        var front = Copy(DatabentoContractRole.VxFrontMonth, vxSources[0], existing, now, changedBy);
        var second = Copy(DatabentoContractRole.VxSecondMonth, vxSources[1], existing, now, changedBy);

        if (!Equivalent(existing.GetValueOrDefault(DatabentoContractRole.EsQuarterly), es))
            es = await store.UpsertAssignmentAsync(es,
                existing.GetValueOrDefault(DatabentoContractRole.EsQuarterly)?.RowVersion ?? 0, cancellationToken).ConfigureAwait(false);
        else es = existing[DatabentoContractRole.EsQuarterly];

        if (!Equivalent(existing.GetValueOrDefault(DatabentoContractRole.VxFrontMonth), front)
            || !Equivalent(existing.GetValueOrDefault(DatabentoContractRole.VxSecondMonth), second))
        {
            var pair = await store.ReplaceVxAssignmentsAsync(front, second,
                existing.GetValueOrDefault(DatabentoContractRole.VxFrontMonth)?.RowVersion ?? 0,
                existing.GetValueOrDefault(DatabentoContractRole.VxSecondMonth)?.RowVersion ?? 0,
                cancellationToken).ConfigureAwait(false);
            front = pair.Single(value => value.ContractRole == DatabentoContractRole.VxFrontMonth);
            second = pair.Single(value => value.ContractRole == DatabentoContractRole.VxSecondMonth);
        }
        else
        {
            front = existing[DatabentoContractRole.VxFrontMonth];
            second = existing[DatabentoContractRole.VxSecondMonth];
        }

        registrations.ReplaceFuturesRolloverSet("ES", [esSources[0] with { OnTheRun = true, Rollover = true }]);
        registrations.ReplaceFuturesRolloverSet("VX", [
            vxSources[0] with { OnTheRun = true, Rollover = true },
            vxSources[1] with { OnTheRun = false, Rollover = true }
        ]);
        return [es, front, second];
    }

    static FuturesContractV3ReadModel[] Eligible(IReadOnlyList<FuturesContractV3ReadModel> values, DateOnly valueDate)
        => [.. values.Where(value => value.IsValid && value.LastTradeDate >= valueDate)
            .OrderBy(value => value.LastTradeDate).ThenBy(value => value.ContractId, StringComparer.Ordinal)];

    static FuturesRolloverContractAssignment Copy(DatabentoContractRole role, FuturesContractV3ReadModel source,
        IReadOnlyDictionary<DatabentoContractRole, FuturesRolloverContractAssignment> existing,
        DateTime now, string changedBy)
    {
        var prior = existing.GetValueOrDefault(role);
        return new()
        {
            ContractRole = role, RootSymbol = source.Symbol, ContractId = source.ContractId,
            Description = source.Description, LocalSymbol = source.LocalSymbol, SecurityType = source.SecurityType,
            Currency = source.Currency, Exchange = source.Exchange, Multiplier = source.Multiplier,
            LastTradeDate = source.LastTradeDate, NextRolloverDate = source.LastTradeDate,
            SourceContractHash = Hash(source), RowVersion = prior?.RowVersion ?? 0,
            CreatedOnUtc = prior?.CreatedOnUtc ?? now, CreatedBy = prior?.CreatedBy ?? changedBy,
            UpdatedOnUtc = now, UpdatedBy = changedBy
        };
    }

    static bool Equivalent(FuturesRolloverContractAssignment? left, FuturesRolloverContractAssignment right)
        => left is not null && left.ContractId == right.ContractId
            && left.SourceContractHash == right.SourceContractHash && left.NextRolloverDate == right.NextRolloverDate;

    public static string Hash(FuturesContractV3ReadModel source)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\u001f",
            source.ContractId, source.Description, source.Symbol, source.LocalSymbol, source.SecurityType,
            source.Currency, source.Exchange, source.Multiplier, source.LastTradeDate)))).ToLowerInvariant();
}
