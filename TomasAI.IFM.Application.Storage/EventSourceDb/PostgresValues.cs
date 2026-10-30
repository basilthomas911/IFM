namespace TomasAI.IFM.Application.Storage.EventSourceDb;
/// <summary>Compatibility entry point returning provider-neutral JSON metadata.</summary>
public static class PostgresValues
{
    public static object Jsonb(string json) => TomasAI.IFM.Shared.Storage.BindValues.Json(json);
}
