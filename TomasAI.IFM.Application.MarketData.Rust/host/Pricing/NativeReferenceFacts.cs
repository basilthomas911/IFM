using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Securities.Persistence;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using System.Text.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Persistence;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Passive raw database facts. This type has no native runtime, lease,
/// recovery registry, pricing context provider, connection or subscription owner.</summary>
public sealed class NativeReferenceFacts(IDbContextFactory databases,IOptionPricingConventionStore conventions,
    TomasAI.IFM.Application.MarketData.Subscriptions.Persistence.IDurableSubscriptionIntentStore? journal=null,ICompositionRoutePlanStore? plans=null,TomasAI.IFM.Application.MarketData.Contracts.IOptionChainStatisticsStore? statistics=null, SecuritiesReferenceReader? referenceReader=null, SecuritiesOptionDefinitionReader? definitionReader=null):INativeReferenceFacts
{
    readonly NativeDefinitionFacts definitionSnapshots=new(databases, definitionReader: definitionReader);
    readonly NativeJournalFacts? journalSnapshots=journal is not null&&plans is not null?new(journal,plans):null;
    /// <inheritdoc/>
    public async Task<byte[]> ReadAsync(byte kind,ReadOnlyMemory<byte> arguments,CancellationToken cancellationToken)
    {
        using var input=JsonDocument.Parse(arguments,new JsonDocumentOptions{MaxDepth=8});var value=input.RootElement;
        switch(kind)
        {
            case 1:
                var from=DateOnly.FromDayNumber(value.GetProperty("from").GetInt32());var until=DateOnly.FromDayNumber(value.GetProperty("until").GetInt32());
                if(until<from || until.DayNumber-from.DayNumber>2000)throw new InvalidDataException("Invalid raw calendar range.");
                var dates=await databases.MarketDataDb.GetTradingDatesAsync(from,until,MarketType.Futures,CurrencyType.USD,cancellationToken).ConfigureAwait(false);
                return JsonSerializer.SerializeToUtf8Bytes(new{dates=dates.Select(d=>d.DayNumber)});
            case 2:
                if(value.TryGetProperty("contracts",out var batch))
                {
                    if(batch.ValueKind!=JsonValueKind.Array || batch.GetArrayLength() is <1 or >16)throw new InvalidDataException("Invalid raw convention batch.");
                    var identities = new (string ContractId, string Version)[batch.GetArrayLength()];
                    var index = 0;
                    foreach (var entry in batch.EnumerateArray())
                    {
                        var id = entry.GetProperty("contract").GetString();
                        var requested = entry.GetProperty("version").GetString();
                        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || string.IsNullOrWhiteSpace(requested) || requested.Length > 512)
                            throw new InvalidDataException("Invalid raw convention identity.");
                        identities[index++] = (id, requested);
                    }
                    var resolved = await conventions.GetManyAsync(identities, cancellationToken).ConfigureAwait(false);
                    return JsonSerializer.SerializeToUtf8Bytes(resolved.Select(exact => exact is null
                        ? null : NativeReferenceProjection.Convention(exact)));
                }
                var contract=value.GetProperty("contract").GetString();var version=value.GetProperty("version").GetString();
                if(string.IsNullOrWhiteSpace(contract)||contract.Length>128||string.IsNullOrWhiteSpace(version)||version.Length>512)throw new InvalidDataException("Invalid raw convention identity.");
                var convention=await conventions.GetAsync(contract,version,cancellationToken).ConfigureAwait(false);
                return JsonSerializer.SerializeToUtf8Bytes(convention is null?null:NativeReferenceProjection.Convention(convention));
            case 3:
                var stageContract=value.GetProperty("contract").GetString();var stageVersion=value.GetProperty("version").GetString();
                var stageBytes=value.GetProperty("model").GetBytesFromBase64();
                if(stageBytes.Length is 0 or >8192)throw new InvalidDataException("Raw option staging model exceeds capacity.");
                var model=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel>(stageBytes,
                    MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
                if(model is null||model.ContractId!=stageContract||model.MappingVersion!=stageVersion)throw new InvalidDataException("Raw option staging identity conflict.");
                var securities=databases.SecuritiesDb;
                if(await (referenceReader?.GetReferenceVersionAsync(model.ContractId,model.MappingVersion!,cancellationToken)
                    ?? securities.GetReferenceVersionAsync(new ExactReferenceVersion(model.ContractId, model.MappingVersion!),cancellationToken)).ConfigureAwait(false) is null){
                    var pending=await securities.StageReferenceVersionAsync(model,cancellationToken).ConfigureAwait(false);
                    await securities.CommitReferenceVersionAsync(pending,cancellationToken).ConfigureAwait(false);
                }
                var staged=await conventions.GetAsync(model.ContractId,model.MappingVersion!,cancellationToken).ConfigureAwait(false);
                return JsonSerializer.SerializeToUtf8Bytes(staged is null?null:NativeReferenceProjection.Convention(staged));
            case 4:
                var environment=value.GetProperty("environment").GetString();
                if(environment is not("Development" or "Paper" or "Production"))throw new InvalidDataException("Invalid raw strategy environment.");
                var published=await databases.MarketDataDb.ReadPublishedAsync(environment,cancellationToken).ConfigureAwait(false);
                if(published.Length>64)throw new InvalidDataException("Raw strategy policy capacity exceeded.");
                return JsonSerializer.SerializeToUtf8Bytes(published.Select(NativeReferenceProjection.Strategy));
            case 5:
                var root=value.GetProperty("root").GetString();
                var first=DateOnly.FromDayNumber(value.GetProperty("from").GetInt32());var last=DateOnly.FromDayNumber(value.GetProperty("until").GetInt32());
                if(string.IsNullOrWhiteSpace(root)||root.Length>60||last<first||last.DayNumber-first.DayNumber>2000)throw new InvalidDataException("Invalid raw option definition range.");
                var definitions = new List<object?[]>();
                var publishedRows = definitionReader?.ReadPublishedOptionDefinitionsAsync(root, first, last, 2048, cancellationToken: cancellationToken)
                    ?? databases.SecuritiesDb.ReadPublishedOptionDefinitionsAsync(new OptionDefinitionExpiryRange(root, first, last, 2048), cancellationToken: cancellationToken);
                await foreach (var row in publishedRows.ConfigureAwait(false))
                {
                    if (definitions.Count == 2048)
                        throw new InvalidDataException("Raw option definition capacity exceeded.");
                    definitions.Add(NativeReferenceProjection.Option(row.Definition));
                }
                return JsonSerializer.SerializeToUtf8Bytes(definitions);
            case 6:
                if(value.TryGetProperty("source",out _))
                    return await definitionSnapshots.ReadSelectedAsync(value,cancellationToken).ConfigureAwait(false);
                var canonical=value.GetProperty("contract").GetString();
                if(string.IsNullOrWhiteSpace(canonical)||canonical.Length>1024)throw new InvalidDataException("Invalid raw canonical option identity.");
                var raw=value.TryGetProperty("version",out var immutableVersion)
                    ?(await ReadVersionAsync(canonical,immutableVersion.GetString()??throw new InvalidDataException("Missing immutable version."),cancellationToken).ConfigureAwait(false))?.Option
                    :await databases.SecuritiesDb.GetFuturesOptionContractAsync(canonical,cancellationToken).ConfigureAwait(false);
                return JsonSerializer.SerializeToUtf8Bytes(raw is null?null:NativeReferenceProjection.Option(raw));
            case 7: return await definitionSnapshots.ReadAsync(value,cancellationToken).ConfigureAwait(false);
            case 8: return await new NativeBrowserFacts(databases).ReadAsync(value,cancellationToken).ConfigureAwait(false);
            case 9: return await (journalSnapshots??throw new InvalidOperationException("Raw committed-journal persistence is not configured.")).ReadAsync(value,cancellationToken).ConfigureAwait(false);
            case 10: return await new NativeStatisticsFacts(statistics??throw new InvalidOperationException("Raw statistics persistence is not configured.")).ReadAsync(value,cancellationToken).ConfigureAwait(false);
            default:throw new InvalidDataException("Unsupported raw database fact kind.");
        }
    }

    Task<TomasAI.IFM.Domain.Reference.Shared.ViewModels.ReferenceContractVersion?> ReadVersionAsync(string id, string version, CancellationToken token)
        => referenceReader?.GetReferenceVersionAsync(id, version, token)
            ?? databases.SecuritiesDb.GetReferenceVersionAsync(new ExactReferenceVersion(id, version), token);

}
