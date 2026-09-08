using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>First-writer immutable market capture. The workflow actor separately accepts its reference under revision fencing.</summary>
public sealed class CompositionPreparationStore(IObjectRepository db) : ICompositionPreparationStore
{
    public const string CreateTable = """
        CREATE TABLE IF NOT EXISTS composition_preparation (
          workflow_id uuid, input_revision bigint, payload blob,
          PRIMARY KEY ((workflow_id), input_revision));
        """;
    const string Select = "SELECT payload FROM composition_preparation WHERE workflow_id=:workflow AND input_revision=:revision;";
    const string Insert = "INSERT INTO composition_preparation(workflow_id,input_revision,payload) VALUES(:workflow,:revision,:payload) IF NOT EXISTS;";

    public async Task<CompositionPreparation?> ReadAsync(CompositionPreparationKey key, CancellationToken cancellationToken)
    {
        CompositionPreparationService.ValidateKey(key);
        var rows = await db.Use("CompositionPreparation.Read", Select).SetParameters(new Parameters([key.WorkflowId, key.InputRevision]))
            .ExecuteQueryAsync(row => MessagePackBinarySerializer.Shared.Deserialize<CompositionPreparation>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
        var result = rows.SingleOrDefault();
        if (result is not null)
        {
            CompositionPreparationService.Validate(result);
            if (result.Key != key) throw new InvalidOperationException("Workflow preparation conflicts with the accepted input hash.");
        }
        return result;
    }

    public async Task<CompositionPreparation> CommitAsync(CompositionPreparation proposed, CancellationToken cancellationToken)
    {
        CompositionPreparationService.Validate(proposed);
        await db.Use("CompositionPreparation.Commit", Insert).SetParameters(new Parameters([
                proposed.Key.WorkflowId, proposed.Key.InputRevision, MessagePackBinarySerializer.Shared.Serialize(proposed)!]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        return await ReadAsync(proposed.Key, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Committed composition preparation could not be read back.");
    }
    readonly record struct Parameters(object[] Values) : IBindValue { public object Bind() => Values; }
}
