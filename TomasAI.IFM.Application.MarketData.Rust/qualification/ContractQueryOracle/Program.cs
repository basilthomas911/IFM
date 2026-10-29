using System.Reflection;
using System.Text.Json;
// Exercise the unchanged CLR native-detail converter, not the different stored
// JSON summary parser. Native bridge flags are calculated from its pinned Rust
// calendar semantics, including its year-zero/CLR date incompatibility.
var assembly = Assembly.Load("TomasAI.IFM.Framework.MarketData.DataBento");
var native = assembly.GetType("TomasAI.IFM.Framework.MarketData.DataBento.NativeContractDetail", true)!;
var flags = assembly.GetType("TomasAI.IFM.Framework.MarketData.DataBento.NativeContractDetailFlags", true)!;
var method = assembly.GetType("TomasAI.IFM.Framework.MarketData.DataBento.DatabentoMarketDataQueries", true)!
    .GetMethod("GetMaturityDate", BindingFlags.Static | BindingFlags.NonPublic)!;
var cases = new List<object>();
foreach (ushort year in new ushort[] { 0, 1, 2024, 2026, 9999, 10000, 65535 })
foreach (byte month in new byte[] { 0, 2, 12, 13 })
foreach (byte day in new byte[] { 0, 28, 29, 31, 32 })
foreach (ulong expiration in new ulong[] { 0, 1798191000000000000, ulong.MaxValue })
{
    var item = Activator.CreateInstance(native)!;
    bool validRustDate = year <= 9999 && month is >= 1 and <= 12 && day >= 1
        && day <= (year == 0 ? new DateTime(2000, month, 1).AddMonths(1).AddDays(-1).Day : DateTime.DaysInMonth(Math.Min((int)year, 9999), month));
    void Set(string name, object value) => native.GetField(name)!.SetValue(item, value);
    Set("Flags", Enum.ToObject(flags, (validRustDate ? 32 : 0) | (expiration != ulong.MaxValue ? 8 : 0)));
    Set("MaturityYear", year); Set("MaturityMonth", month); Set("MaturityDay", day); Set("ExpirationTimestampNanoseconds", expiration);
    string? maturity = null; bool invalid = false;
    try { maturity = ((DateOnly?)method.Invoke(null, [item]))?.ToString("yyyy-MM-dd"); }
    catch (TargetInvocationException error) when (error.InnerException is TomasAI.IFM.Framework.MarketData.DataBento.DatabentoFeedException) { invalid = true; }
    var json = JsonSerializer.Serialize(new { hd = new { rtype = 19, publisher_id = 1, instrument_id = 42, ts_event = "0" }, raw_symbol = "ESZ6", asset = "ES", instrument_class = "F", underlying_id = 0, ts_recv = "0", expiration = expiration.ToString(), maturity_year = year, maturity_month = month, maturity_day = day });
    cases.Add(new { json, maturity, invalid });
}
File.WriteAllText(args[0], JsonSerializer.Serialize(cases));
Console.WriteLine($"Unchanged C# native contract-query maturity: {cases.Count} date/conflict/sentinel cases.");
