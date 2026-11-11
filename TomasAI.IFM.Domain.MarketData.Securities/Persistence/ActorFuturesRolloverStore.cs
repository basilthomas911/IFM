using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Prepares rollover facts from committed projections and submits them to their catalog actor.</summary>
public sealed class ActorFuturesRolloverStore(ISecuritiesDbContext database, IActorProducer producer) : IFuturesContractRolloverStore
{
    static readonly ActorMailboxId Publisher = new(ActorType.Command, "FuturesRolloverPreparation");

    /// <inheritdoc/>
    public Task EnsureFuturesContractRolloverRowsAsync(IReadOnlyCollection<string> symbols, DateTime createdOnUtc, string createdBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        var normalized = symbols.Select(static symbol => symbol?.Trim().ToUpperInvariant())
            .Where(static symbol => !string.IsNullOrWhiteSpace(symbol)).Distinct(StringComparer.Ordinal)
            .Select(static symbol => symbol!).ToArray();
        if (normalized.Length == 0)
            throw new ArgumentException("At least one futures symbol is required.", nameof(symbols));
        return database.DbWriter.EnsureFuturesContractRolloverRowsAsync(normalized, createdOnUtc, createdBy, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<FuturesContractRolloverReadModel?> GetFuturesContractRolloverAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return database.DbReader.GetFuturesContractRolloverAsync(symbol.Trim().ToUpperInvariant(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<FuturesContractRolloverReadModel>> GetFuturesContractRolloversAsync(CancellationToken cancellationToken = default)
        => database.DbReader.GetFuturesContractRolloversAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<FuturesContractV3ReadModel?> GetPersistedFuturesContractAsync(string contractId, CancellationToken cancellationToken = default)
        => database.DbReader.GetPersistedFuturesContractAsync(contractId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<FuturesContractV3ReadModel>> GetFuturesRolloverSetAsync(string symbol, CancellationToken cancellationToken = default)
        => database.DbReader.GetFuturesRolloverSetAsync(symbol, cancellationToken);

    /// <inheritdoc/>
    public Task ReplaceOnTheRunFuturesContractAsync(FuturesContractRolloverReadModel rollover, FuturesContractV3ReadModel contract, CancellationToken cancellationToken = default)
        => ReplaceFuturesRolloverSetAsync(rollover, [contract], cancellationToken);

    /// <inheritdoc/>
    public async Task ReplaceFuturesRolloverSetAsync(FuturesContractRolloverReadModel rollover, IReadOnlyCollection<FuturesContractV3ReadModel> contracts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rollover);
        ArgumentNullException.ThrowIfNull(contracts);
        if (contracts.Count == 0 || contracts.Any(contract => contract is null))
            throw new ArgumentException("A non-empty rollover set with complete rows is required.", nameof(contracts));
        var ordered = FuturesRolloverPreparation.Order(contracts);
        var pointer = rollover with { Symbol = rollover.Symbol.Trim().ToUpperInvariant() };
        if (ordered.Length == 0 || ordered.Any(contract => !contract.Rollover)
            || !FuturesRolloverPreparation.IsValid(pointer, ordered, ordered[0].Id))
            throw new ArgumentException("The rollover row must identify the one on-the-run contract in a distinct rollover set for the same symbol.", nameof(contracts));

        var activeIds = ordered.Select(contract => contract.Id).ToHashSet();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var revision = await database.DbReader.ReadCatalogRevisionAsync(SecuritiesCatalogStreams.Futures, cancellationToken).ConfigureAwait(false);
            var existing = await database.DbReader.GetFuturesRolloverSetAsync(pointer.Symbol, cancellationToken).ConfigureAwait(false);
            var current = await database.DbReader.GetFuturesContractRolloverAsync(pointer.Symbol, cancellationToken).ConfigureAwait(false);
            var verified = await database.DbReader.ReadCatalogRevisionAsync(SecuritiesCatalogStreams.Futures, cancellationToken).ConfigureAwait(false);
            if (revision != verified) continue;
            if (current is not null && current.ContractId == pointer.ContractId && current.NextRolloverDate == pointer.NextRolloverDate
                && FuturesRolloverPreparation.Order(existing).SequenceEqual(ordered)) return;

            var retired = existing.Where(contract => !activeIds.Contains(contract.Id))
                .Select(contract => contract with { Rollover = false, OnTheRun = false });
            FuturesContractV3ReadModel[] changed = [.. FuturesRolloverPreparation.Order(retired), .. ordered];
            var body = JsonSerializer.SerializeToUtf8Bytes(new { revision, pointer, changed });
            var command = new ReplaceFuturesRolloverSetCommand
            {
                CommandId = new Guid(SHA256.HashData(body).AsSpan(0, 16)),
                Subject = new(ActorType.Command, AddFuturesContractCommand.Actor, ReplaceFuturesRolloverSetCommand.Verb, ordered[0].Id.Format()),
                PostEvents = true, EntityId = ordered[0].Id, ExpectedCatalogRevision = revision,
                Rollover = pointer, ChangedContracts = changed
            };
            await producer.StartAsync(Publisher, cancellationToken).ConfigureAwait(false);
            var result = await producer.RequestAsync<ReplaceFuturesRolloverSetCommand, FuturesContractId, GuidResult>(
                command.Subject, command, command.EntityId, cancellationToken).ConfigureAwait(false);
            if (result.Success) return;
            if (result.ErrorCode != ReplaceFuturesRolloverSetCommand.CatalogChanged)
                throw new InvalidOperationException($"Securities rejected rollover for {pointer.Symbol} ({result.ErrorCode}): {result.ErrorMessage}");
        }
        throw new InvalidOperationException($"The futures catalog changed repeatedly while preparing rollover for {pointer.Symbol}.");
    }
}

/// <summary>Pure rollover preparation rules shared by the producer and owning actor.</summary>
internal static class FuturesRolloverPreparation
{
    internal static FuturesContractV3ReadModel[] Order(IEnumerable<FuturesContractV3ReadModel> contracts)
        => contracts.OrderBy(contract => contract.LastTradeDate).ThenBy(contract => contract.ContractId, StringComparer.Ordinal).ToArray();

    internal static bool IsValid(FuturesContractRolloverReadModel? pointer, FuturesContractV3ReadModel[]? changed, FuturesContractId frontId)
    {
        if (pointer is null || changed is not { Length: > 0 } || string.IsNullOrWhiteSpace(pointer.Symbol)
            || string.IsNullOrWhiteSpace(pointer.ContractId) || pointer.NextRolloverDate is null || pointer.UpdatedOn is null
            || string.IsNullOrWhiteSpace(pointer.UpdatedBy)) return false;
        if (changed.Any(contract => contract is null || !StringComparer.Ordinal.Equals(contract.Symbol, pointer.Symbol)
            || (contract.OnTheRun && !contract.Rollover))
            || changed.Select(contract => contract.ContractId).Distinct(StringComparer.Ordinal).Count() != changed.Length) return false;
        var active = Order(changed.Where(contract => contract.Rollover));
        if (active.Length == 0 || active.Count(contract => contract.OnTheRun) != 1 || !active[0].OnTheRun) return false;
        return active[0].Id == frontId && active[0].ContractId == pointer.ContractId && active[0].LastTradeDate == pointer.NextRolloverDate
            && (pointer.Symbol != "ES" || active.Length == 1) && (pointer.Symbol != "VX" || active.Length == 2);
    }
}
