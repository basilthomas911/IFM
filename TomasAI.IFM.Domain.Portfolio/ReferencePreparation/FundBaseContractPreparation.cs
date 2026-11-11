using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.ReferencePreparation;

/// <summary>Initializes Fund contracts through their owning actors before issuing the preparation receipt.</summary>
public sealed class FundBaseContractPreparation(IDbContextFactory database, IActorSupervisor supervisor,
    IValueDateProvider valueDates, TimeProvider clock)
{
    /// <summary>Uses one published definition generation, preserves current selections, and observes every committed projection.</summary>
    public async Task EnsureAsync(ValidatePortfolioReferenceCommand request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        token.ThrowIfCancellationRequested();
        var rules = request.Mandate?.BaseContracts ?? [];
        if (rules.Length == 0) return;
        if (rules.Select(x => x.Symbol).Distinct(StringComparer.Ordinal).Count() != rules.Length)
            throw new ArgumentException("Select each Fund base contract symbol only once.", nameof(request));
        var reference = database.ReferenceDb;
        var snapshot = await reference.GetInstrumentDefinitionSnapshotAsync(token).ConfigureAwait(false);
        if (snapshot is null || !await reference.IsInstrumentDefinitionSelectionCompleteAsync(snapshot.Id, token).ConfigureAwait(false))
            throw new InvalidOperationException("Published Databento definitions are unavailable; Fund defaults were not saved.");
        var products = await reference.GetInstrumentDefinitionSymbolsAsync(snapshot.Id, TradeStrategyFamilyType.Futures, token).ConfigureAwait(false);
        var plans = new List<(FuturesContractV3ReadModel Contract, FuturesContractV3ReadModel? Previous)>();
        var day = valueDates.ValueDate;
        var now = clock.GetUtcNow();
        foreach (var rule in rules)
        {
            var matches = products.Where(x => x.Symbol == rule.Symbol).ToArray();
            if (rule.Validate().Count != 0 || matches.Length != 1)
                throw new InvalidOperationException($"{rule.Symbol}: select an unambiguous published Databento Futures symbol.");
            var definitions = new List<InstrumentDefinitionSelection>();
            foreach (var dataset in snapshot.Datasets)
            {
                var after = string.Empty;
                for (var page = 0; ; page++)
                {
                    var rows = await reference.GetInstrumentDefinitionSelectionAsync(snapshot.Id, dataset, rule.Symbol, false, after, 200, token).ConfigureAwait(false);
                    if (rows.Any(x => x.Value.SnapshotId != snapshot.Id || x.Value.Dataset != dataset))
                        throw new InvalidDataException("Databento contract provenance does not match the published definition generation.");
                    definitions.AddRange(rows.Select(x => x.Value));
                    if (rows.Count < 200) break;
                    if (page == 99 || rows[^1].Key == after) throw new InvalidDataException("Unbounded Databento contract selection.");
                    after = rows[^1].Key;
                }
            }
            var existing = await database.SecuritiesDb.GetFuturesContractsBySymbolAsync(rule.Symbol, token).ConfigureAwait(false);
            for (var i = 0; i < definitions.Count; i++)
            {
                var selection = definitions[i];
                if (selection.Multiplier is not null || selection.Deleted || selection.InstrumentClass != "F" ||
                    HasExistingIdentity(selection, existing)) continue;
                var key = selection.RawDefinitionReference.Split(':');
                if (key.Length != 5 || key[0] != "instrument_definition" || key[1] != snapshot.Id.ToString("N") || key[2] != selection.Dataset ||
                    !int.TryParse(key[3], CultureInfo.InvariantCulture, out var bucket) || !long.TryParse(key[4], CultureInfo.InvariantCulture, out var index))
                    throw new InvalidDataException("Invalid immutable Databento definition reference.");
                InstrumentDefinitionRequestQualification.RequireOrdinal(bucket, index);
                var json = await reference.GetInstrumentDefinitionJsonAsync(snapshot.Id, selection.Dataset, bucket, index, token).ConfigureAwait(false)
                    ?? throw new InvalidDataException("The exact Databento definition is unavailable.");
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), selection.DefinitionDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Databento definition content does not match its recorded digest.");
                var exact = ExactInstrumentDefinition.Parse(selection.Dataset, json);
                var verified = InstrumentDefinitionSelectionMapper.Map(snapshot.Id, index, exact);
                if (verified.InstrumentId != selection.InstrumentId || verified.PublisherId != selection.PublisherId || verified.RawSymbol != selection.RawSymbol ||
                    verified.Root != selection.Root || verified.Exchange != selection.Exchange || verified.Currency != selection.Currency ||
                    verified.ExpirationUtc != selection.ExpirationUtc || verified.InstrumentClass != selection.InstrumentClass)
                    throw new InvalidDataException("Databento definition identity does not match its selection.");
                definitions[i] = selection with { Multiplier = verified.Multiplier };
            }
            var selected = Plan(rule, matches[0], definitions, existing, day, now);
            foreach (var retired in existing.Where(x => x.OnTheRun && !selected.Any(y => y.ContractId == x.ContractId && y.OnTheRun)))
                plans.Add((retired with { OnTheRun = false, Rollover = false }, retired));
            foreach (var contract in selected)
                plans.Add((contract, existing.SingleOrDefault(x => x.ContractId == contract.ContractId)));
        }
        // Validate every symbol before sending any catalog command. Retries can safely
        // retain already committed reference rows if a later command fails.
        var producer = supervisor.GetProducer(new(ActorType.Function, ValidatePortfolioReferenceCommand.Actor));
        foreach (var (contract, previous) in plans)
        {
            token.ThrowIfCancellationRequested();
            if (contract == previous) continue;
            var operation = previous is null ? "Add" : "Change";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture,
                $"FundBaseContracts/{request.OwnerCommandId:N}/{operation}/{System.Text.Json.JsonSerializer.Serialize(contract)}")));
            var commandId = new Guid(bytes.AsSpan(0, 16));
            ServiceResult<GuidResult> result;
            if (previous is null)
            {
                var command = new AddFuturesContractCommand(contract) { CommandId = commandId, PostEvents = true, ErrorCode = AddFuturesContractCommand.ErrorId,
                    Subject = new(ActorType.Command, AddFuturesContractCommand.Actor, AddFuturesContractCommand.Verb, contract.Id.Format()) };
                result = await producer.RequestAsync<AddFuturesContractCommand, FuturesContractId, GuidResult>(command.Subject, command, command.EntityId, token).ConfigureAwait(false);
            }
            else
            {
                var command = new ChangeFuturesContractCommand(previous.Id, contract, false) { CommandId = commandId, PostEvents = true, ErrorCode = ChangeFuturesContractCommand.ErrorId,
                    Subject = new(ActorType.Command, ChangeFuturesContractCommand.Actor, ChangeFuturesContractCommand.Verb, contract.Id.Format()) };
                result = await producer.RequestAsync<ChangeFuturesContractCommand, FuturesContractId, GuidResult>(command.Subject, command, command.EntityId, token).ConfigureAwait(false);
            }
            if (!result.Success)
                throw new InvalidOperationException($"{contract.Symbol}: securities initialization failed ({result.ErrorCode}): {result.ErrorMessage}. Fund defaults were not saved.");
        }
        foreach (var (contract, _) in plans)
        {
            var observed = false;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var row = await database.SecuritiesDb.GetFuturesContractAsync(contract.ContractId, token).ConfigureAwait(false);
                if (row == contract) { observed = true; break; }
                await Task.Delay(TimeSpan.FromMilliseconds(50), clock, token).ConfigureAwait(false);
            }
            if (!observed) throw new InvalidOperationException($"{contract.ContractId}: securities initialization is not yet projected. Retry Save; Fund defaults were not saved.");
        }
    }

    /// <summary>Prepares the current plus N unexpired provider outrights without changing existing contract economics.</summary>
    public static FuturesContractV3ReadModel[] Plan(FundBaseContractRule rule, TradeStrategySymbolReadModel product,
        IEnumerable<InstrumentDefinitionSelection> definitions, ICollection<FuturesContractV3ReadModel> existing,
        DateOnly day, DateTimeOffset now)
    {
        if (rule.Validate().Count != 0) throw new ArgumentException("Invalid Fund base contract rule.", nameof(rule));
        var rows = definitions.Where(x => !x.Deleted && x.InstrumentClass == "F" && x.Root == rule.Symbol &&
            x.Exchange == product.Exchange && x.Currency == product.Currency && x.InstrumentId != 0 &&
            x.ExpirationUtc is { } expiry && expiry > now && DateOnly.FromDateTime(expiry.UtcDateTime) >= day &&
            (x.ActivationUtc is null || x.ActivationUtc <= now) && (x.Multiplier > 0 || HasExistingIdentity(x, existing)) && x.TickSize > 0 &&
            !string.IsNullOrWhiteSpace(x.RawSymbol) && !string.IsNullOrWhiteSpace(x.Dataset) &&
            !string.IsNullOrWhiteSpace(x.DefinitionDigest) && !string.IsNullOrWhiteSpace(x.RawDefinitionReference))
            .GroupBy(x => DateOnly.FromDateTime(x.ExpirationUtc!.Value.UtcDateTime))
            .Select(x => x.OrderByDescending(y => y.DefinitionTimestampUtc).First()).OrderBy(x => x.ExpirationUtc).ToArray();
        var current = existing.Where(x => x.OnTheRun && x.Rollover && rows.Any(r => DateOnly.FromDateTime(r.ExpirationUtc!.Value.UtcDateTime) == x.LastTradeDate)).ToArray();
        if (current.Length > 1) throw new InvalidDataException($"{rule.Symbol}: multiple current contracts require reconciliation.");
        var start = current.SingleOrDefault()?.LastTradeDate ?? (rows.Length == 0 ? DateOnly.MaxValue : DateOnly.FromDateTime(rows[0].ExpirationUtc!.Value.UtcDateTime));
        var selected = rows.Where(x => DateOnly.FromDateTime(x.ExpirationUtc!.Value.UtcDateTime) >= start).Take(rule.ForwardCycles + 1).ToArray();
        if (selected.Length != rule.ForwardCycles + 1)
            throw new InvalidOperationException($"{rule.Symbol}: Databento has {selected.Length} qualified contracts; {rule.ForwardCycles + 1} are required for the current contract plus {rule.ForwardCycles} forward cycles.");
        return selected.Select(definition =>
        {
            var date = DateOnly.FromDateTime(definition.ExpirationUtc!.Value.UtcDateTime);
            var id = string.Create(CultureInfo.InvariantCulture, $"{rule.Symbol}{date:yyyyMMdd}");
            var old = existing.SingleOrDefault(x => x.LastTradeDate == date && (x.ContractId == id || x.RawSymbol == definition.RawSymbol || x.LocalSymbol == definition.RawSymbol));
            if (old is not null)
                return current.Length != 0 || date != start ? old : old with { OnTheRun = true, Rollover = true };
            return new FuturesContractV3ReadModel(id, string.Create(CultureInfo.InvariantCulture, $"{product.Description} - {date:yyyy-MMM-dd}"),
                rule.Symbol, definition.RawSymbol, "FUT", definition.Currency, definition.Exchange,
                definition.Multiplier!.Value.ToString(CultureInfo.InvariantCulture), date, date == start, date == start)
            {
                SchemaVersion = 1, ReviewState = ReferenceReviewState.Draft,
                Dataset = definition.Dataset, InstrumentId = definition.InstrumentId, PublisherId = definition.PublisherId,
                RawSymbol = definition.RawSymbol, DefinitionTimestampUtc = definition.DefinitionTimestampUtc,
                DefinitionDigest = definition.DefinitionDigest, RawDefinitionReference = definition.RawDefinitionReference,
                ExpirationUtc = definition.ExpirationUtc, MultiplierValue = definition.Multiplier, TickSize = definition.TickSize
            };
        }).ToArray();
    }

    /// <summary>Recognizes an already owned contract without replacing its reviewed economics.</summary>
    static bool HasExistingIdentity(InstrumentDefinitionSelection definition, ICollection<FuturesContractV3ReadModel> existing)
        => definition.ExpirationUtc is { } expiry && existing.Any(x => x.Symbol == definition.Root &&
            x.Exchange == definition.Exchange && x.Currency == definition.Currency && x.LastTradeDate == DateOnly.FromDateTime(expiry.UtcDateTime) &&
            (x.RawSymbol == definition.RawSymbol || x.LocalSymbol == definition.RawSymbol ||
                x.ContractId == string.Create(CultureInfo.InvariantCulture, $"{definition.Root}{x.LastTradeDate:yyyyMMdd}")));
}
