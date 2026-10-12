using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
public sealed record CatalogCommandRequest(Guid OperationId, CatalogCommandOperation Operation,
    StrategyCatalogDefinition? Definition = null, CatalogKey? Key = null, int ExpectedPreviousVersion = 0,
    string? ExpectedHash = null, DateTime? EffectiveUtc = null);
