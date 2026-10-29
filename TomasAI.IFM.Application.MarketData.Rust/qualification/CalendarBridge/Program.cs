using System.Collections.Immutable;
using System.Text.Json;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;
// Freeze the platform's supported Windows-to-IANA identifier translation as
// configuration data. Runtime Rust uses pinned IANA rules, never OS date APIs.
var mappings=new SortedDictionary<string,string>(StringComparer.Ordinal);
foreach(var zone in TimeZoneInfo.GetSystemTimeZones())
    if(TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id,out var iana))mappings[zone.Id]=iana;
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(mappings,new JsonSerializerOptions{WriteIndented=true}));
var rows=new List<object>();
var from=new DateOnly(2025,1,1);var until=new DateOnly(2027,12,31);
var dates=Enumerable.Range(0,until.DayNumber-from.DayNumber+1).Select(from.AddDays).Where(d=>d.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray();
var expiry=new DateTimeOffset(2026,12,31,16,0,0,TimeSpan.Zero);
foreach(var zone in new[]{"America/Chicago","America/New_York","UTC","Europe/London","Asia/Tokyo","Australia/Sydney","Central Standard Time","Eastern Standard Time"})
foreach(var day in new[]{new DateOnly(2026,3,8),new DateOnly(2026,11,1),new DateOnly(2026,10,27)})
foreach(var hour in Enumerable.Range(0,24))
foreach(var delta in new[]{-1L,0L,1L}) {
    var at=new DateTimeOffset(day.ToDateTime(new TimeOnly(hour,0)),TimeSpan.Zero).AddTicks(delta);
    var calendar=new OptionPricingCalendar("fixture/v1",zone,from,until,new(18,0),dates);
    var contract=new OptionPricingConvention {ContractId="ES20261231C6000",Dataset="GLBX.MDP3",PublisherId=3,InstrumentId=42,RawSymbol="fixture",Root="ES",Exchange="XCME",Currency="USD",
        UnderlyingContractId="ES20261218",ExerciseStyle=OptionExerciseStyle.European,SettlementStyle=OptionSettlementStyle.DeliveryOfFuture,ExpirationUtc=expiry,LastTradingUtc=expiry,
        DayCount=PricingDayCount.Actual365Fixed,CalendarVersion=calendar.Version,Multiplier=50,TickSize=.25m,TickRuleVersion="fixture",DefinitionDigest=new('a',64),MappingVersion="fixture",EvidenceId="fixture",
        EffectiveFromUtc=new(2025,1,1,0,0,0,TimeSpan.Zero),EffectiveUntilUtc=new(2027,1,1,0,0,0,TimeSpan.Zero)};
    var count=OptionPricingQualification.CountTradingDays(calendar,contract,at);
    var local=TimeZoneInfo.ConvertTime(at,TimeZoneInfo.FindSystemTimeZoneById(zone));
    var localDate=DateOnly.FromDateTime(local.DateTime);if(TimeOnly.FromDateTime(local.DateTime)>=calendar.ValueDateRollover)localDate=localDate.AddDays(1);
    rows.Add(new{zone,at=at.UtcTicks-DateTime.UnixEpoch.Ticks,expiry=expiry.UtcTicks-DateTime.UnixEpoch.Ticks,count,valueDate=localDate.DayNumber});
}
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS exported {mappings.Count} platform zone aliases and {rows.Count} actual C# calendar/rollover fixtures.");
