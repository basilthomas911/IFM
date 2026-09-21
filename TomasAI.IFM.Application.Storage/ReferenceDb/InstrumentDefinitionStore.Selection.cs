using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

public sealed partial class InstrumentDefinitionStore
{
    public const string CreateSelectionStatusTable = """
        CREATE TABLE IF NOT EXISTS instrument_definition_selection_status(snapshot_id uuid PRIMARY KEY, complete boolean);
        """;
    public const string CreateSelectionTable = """
        CREATE TABLE IF NOT EXISTS instrument_definition_selection (
            snapshot_id uuid, dataset text, root text, options boolean, instrument_key text, payload blob,
            PRIMARY KEY ((snapshot_id,dataset,root,options),instrument_key));
        """;
    public async Task IndexSelectionAsync(InstrumentDefinitionSelection value, CancellationToken cancellationToken)
    {
        if (value.SnapshotId == Guid.Empty || value.InstrumentClass is not ("F" or "C" or "P")
            || value.InstrumentId == 0 || value.PublisherId == 0 || string.IsNullOrWhiteSpace(value.Root)
            || value.DefinitionDigest.Length != 64)
            throw new ArgumentException("Invalid provider definition index entry.");
        var key = FormattableString.Invariant($"{value.PublisherId:D5}:{value.InstrumentId:D10}");
        await db.Use("InstrumentDefinition.IndexSelection", """
            INSERT INTO instrument_definition_selection(snapshot_id,dataset,root,options,instrument_key,payload)
            VALUES(:snapshot,:dataset,:root,:options,:key,:payload);
            """).SetParameters(new Parameters([value.SnapshotId, value.Dataset, value.Root, value.InstrumentClass != "F",
                key, MessagePackBinarySerializer.Shared.Serialize(value)!])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<InstrumentDefinitionPage> GetSelectionPageAsync(InstrumentDefinitionPageRequest request,
        DateTimeOffset at, CancellationToken cancellationToken)
    {
        request.Validate();
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Definition catalog unavailable. Refresh stored definitions first.");
        var indexed = await db.Use("InstrumentDefinition.SelectionStatus",
                "SELECT complete FROM instrument_definition_selection_status WHERE snapshot_id=:snapshot;")
            .SetParameters(new Parameters([snapshot.Id])).ExecuteQueryAsync(row => row.GetBool(0), cancellationToken).ConfigureAwait(false);
        if (!indexed.SingleOrDefault()) throw new InvalidOperationException("The stored catalog predates the selector index. Refresh definitions before browsing.");
        // A continuation never drifts to a newly published catalog.
        if (request.SnapshotId != Guid.Empty && request.SnapshotId != snapshot.Id)
            throw new InvalidOperationException("Definition catalog changed. Restart the search.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(request with { SnapshotId = snapshot.Id, ContinuationToken = null }))));
        var after = "";
        if (!string.IsNullOrEmpty(request.ContinuationToken))
        {
            Cursor cursor;
            try { cursor = JsonSerializer.Deserialize<Cursor>(Convert.FromBase64String(request.ContinuationToken))!; }
            catch (Exception e) when (e is FormatException or JsonException)
            { throw new ArgumentException("Invalid definition page cursor.", e); }
            if (cursor is null || cursor.Fingerprint != fingerprint || cursor.After is not { Length: 16 }
                || cursor.After.Any(c => c != ':' && !char.IsAsciiDigit(c)))
                throw new ArgumentException("Definition cursor does not match the search.");
            after = cursor.After;
        }
        var rows = (await db.Use("InstrumentDefinition.SelectionPage", """
            SELECT instrument_key,payload FROM instrument_definition_selection
            WHERE snapshot_id=:snapshot AND dataset=:dataset AND root=:root AND options=:options
            AND instrument_key>:after LIMIT :page_limit;
            """).SetParameters(new Parameters([snapshot.Id, request.Dataset, request.Root, request.Options, after, request.PageSize + 1]))
            .ExecuteQueryAsync(row => (Key: row.GetString(0), Value: MessagePackBinarySerializer.Shared.Deserialize<InstrumentDefinitionSelection>(row.GetBytes(1))!),
                cancellationToken).ConfigureAwait(false)).ToArray();
        var page = rows.Take(request.PageSize).ToArray();
        if (page.Any(x => x.Value is null || x.Value.SnapshotId != snapshot.Id || x.Value.Dataset != request.Dataset
            || x.Value.Root != request.Root || (x.Value.InstrumentClass != "F") != request.Options))
            throw new InvalidDataException("Definition index identity conflict.");
        bool Matches(InstrumentDefinitionSelection x) =>
            (request.IncludeExpiredOrDeleted || !x.Deleted && x.ExpirationUtc > at && (x.ActivationUtc is null || x.ActivationUtc <= at))
            && (request.Exchange is null || x.Exchange == request.Exchange)
            && (request.Expiry is null || x.ExpirationUtc is { } expiry && DateOnly.FromDateTime(expiry.UtcDateTime) == request.Expiry)
            && (request.UnderlyingInstrumentId is null || x.UnderlyingInstrumentId == request.UnderlyingInstrumentId)
            && (request.Right == ReferenceOptionRight.Unknown || x.InstrumentClass == (request.Right == ReferenceOptionRight.Call ? "C" : "P"))
            && (request.MinimumStrike is null || x.Strike >= request.MinimumStrike)
            && (request.MaximumStrike is null || x.Strike <= request.MaximumStrike);
        var next = rows.Length > request.PageSize
            ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Cursor(fingerprint, page[^1].Key))) : null;
        return new(snapshot.Id, snapshot.CompletedUtc, page.Select(x => x.Value).Where(Matches).ToArray(), next);
    }
    sealed record Cursor(string Fingerprint, string After);
}
