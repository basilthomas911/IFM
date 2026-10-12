using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

internal sealed record CatalogChildTable(string Name, string Property, string OwnerCheck, string Columns,
    string KeyColumns, string InsertColumns, string InsertValues, string ReadJson, string Constraints = "");
