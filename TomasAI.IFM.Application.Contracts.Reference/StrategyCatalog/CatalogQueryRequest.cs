using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
public sealed record CatalogQueryRequest(CatalogQueryOperation Operation, StrategyCatalogKind Kind = StrategyCatalogKind.Family,
    CatalogKey? Key = null, int Limit = 100, string? AfterCode = null);
