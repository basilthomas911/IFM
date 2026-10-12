namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

/// <summary>Preserves the former UTC storage interpretation of unspecified timestamps; converts local timestamps to UTC.</summary>
internal static class OptionPricerPostgresTime
{
    internal static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    internal static DateTime? AsUtc(DateTime? value) => value.HasValue ? AsUtc(value.Value) : null;
}
