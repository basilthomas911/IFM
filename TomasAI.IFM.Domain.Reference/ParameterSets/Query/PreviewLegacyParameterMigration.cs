using System.Text.Json;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query;

public static class PreviewLegacyParameterMigration
{
    public static async ValueTask ExecuteAsync(
        this PreviewLegacyParameterMigrationQuery query,
        IParameterSetQueryContext context,
        ILogger<ParameterSetQueryActor> logger,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        context.AccessPolicy.Demand(ParameterCapability.Read);
        if (query.SetId == Guid.Empty || query.Version <= 0)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("PARAM.LEGACY_IDENTITY_INVALID");
#pragma warning restore MA0015

        var row = (await (context.ConfigurationDb.GetLegacyParameterVersionsAsync(query.SetId, query.Version, cancellationToken: token)).ConfigureAwait(false)).SingleOrDefault()
            ?? throw new InvalidOperationException("PARAM.LEGACY_NOT_FOUND");
        var json = ParameterLegacyMigrationModel.Expand(row);
        var values=ParameterHistoryReader.ReadValues(ParameterSchemaRegistry.RegimeComponent,json);
        var id = ParameterLegacyMigrationModel.TargetId(row.Reference);
        var command = new CreateParameterSetCommand
        {
            CommandId = id,
            EntityId = new(id),
            Name = $"Legacy Regime {row.Reference.SetId:N} v{row.Reference.Version}",
            Description = row.Description.Length <= 4000 ? row.Description : row.Description[..4000],
            SchemaVersion = values.SchemaVersion,
            Parameters = values,
            LegacySource = row.Reference
        };
        await (context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<CreateParameterSetCommand>(command))).ConfigureAwait(false);
    }
}
