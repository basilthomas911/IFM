using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
public static class StrategyCatalogUris
{
    public const string Query = "/api/reference/strategy-catalog/query";
    public const string Command = "/api/reference/strategy-catalog/command";
}
