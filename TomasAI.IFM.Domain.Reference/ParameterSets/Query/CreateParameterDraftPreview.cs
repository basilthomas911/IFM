using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query;

public static class CreateParameterDraftPreview
{
    public static async ValueTask ExecuteAsync(this CreateParameterDraftPreviewQuery query, IParameterSetQueryContext context, ILogger<ParameterSetQueryActor> logger, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
        context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
        var descriptor = ParameterComponentModelRegistry.Get(query.ComponentCode);
        if (!string.Equals(query.PayloadJson,"{}",StringComparison.Ordinal))
        {
            using var source = System.Text.Json.JsonDocument.Parse(ParameterCanonicalPayloadModel.Canonicalize(query.PayloadJson));
            var version = source.RootElement.GetProperty("SchemaVersion").GetInt32();
            if (!ParameterSchemaRegistry.Default.CanEditLosslessly(query.ComponentCode, version, query.PayloadJson))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
                throw new ArgumentException("PARAM.EDITOR_UNSUPPORTED_FIELDS: The original payload is preserved and cannot be edited by this editor.");
#pragma warning restore MA0015
        }
        string result;
        if (string.Equals(query.ComponentCode,RegimeDiscoveryParameterModel.ComponentCode,StringComparison.Ordinal))
        {
            var parameters = string.Equals(query.PayloadJson,"{}",StringComparison.Ordinal)
             ? RegimeDiscoveryParameterModel.CreateExplicitSeed(query.SetId, (TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType)query.TargetHorizon)
             : RegimeDiscoveryParameterModel.UpgradeToExplicitSet(System.Text.Json.JsonSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet>(ParameterCanonicalPayloadModel.Canonicalize(query.PayloadJson))
                ?? throw new ArgumentException("Source parameter payload is required.",nameof(query)), query.RebuildIntervals);
            if (parameters.ParameterSetId != query.SetId) throw new ArgumentException("Source parameter identity does not match the preview request.",nameof(query));
            result = System.Text.Json.JsonSerializer.Serialize(parameters);
        }
        else
        {
            result = string.Equals(query.PayloadJson,"{}",StringComparison.Ordinal)
             ? descriptor.CreateDraftPayload(query.SetId)
             : ParameterCanonicalPayloadModel.Canonicalize(query.PayloadJson);
            using var payload = System.Text.Json.JsonDocument.Parse(result);
            if (!payload.RootElement.TryGetProperty("ParameterSetId", out var setId)
               || setId.GetGuid() != query.SetId)
                throw new ArgumentException("Source parameter identity does not match the preview request.",nameof(query));
        }
        await (context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<string>(result))).ConfigureAwait(false);
    }
}
