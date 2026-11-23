namespace TomasAI.IFM.Application.Storage;

/// <summary>Invalidates process-local read caches after committed publication writes.</summary>
public static class ReadCacheInvalidation
{
    static long configuration;
    static long calendar;
    public static long Configuration => Interlocked.Read(ref configuration);
    public static long Calendar => Interlocked.Read(ref calendar);
    public static void ConfigurationChanged() => Interlocked.Increment(ref configuration);
    public static void CalendarChanged() => Interlocked.Increment(ref calendar);
}
