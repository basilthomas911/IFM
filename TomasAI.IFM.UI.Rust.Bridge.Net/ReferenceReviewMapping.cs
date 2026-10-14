using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Edits only user-reviewed conventions; provider identity, economics, strike and definition evidence remain immutable.</summary>
public static class ReferenceReviewMapping
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public static ContractEditorRow Row(ContractEditorRow row, FuturesContractV3ReadModel contract) => Merge(row,ReferenceConventionEditor.From(contract));
    public static ContractEditorRow Row(ContractEditorRow row, FuturesOptionContractReadModel contract) => Merge(row,ReferenceConventionEditor.From(contract));
    static ContractEditorRow Merge(ContractEditorRow row,ReferenceConventionEditor review)
    {
        var fields=new Dictionary<string,string>(row.Fields);
        foreach(var property in JsonSerializer.SerializeToElement(review,Json).EnumerateObject())fields[property.Name]=property.Value.ValueKind==JsonValueKind.Null?"":property.Value.ToString();
        return row with{Fields=fields};
    }
    public static FuturesContractV3ReadModel Futures(ContractEditorIntent input,FuturesContractV3ReadModel original)
    {
        if(original.SchemaVersion!=1)throw new ArgumentException("Import a provider definition before reviewing this legacy contract");
        var value=Review(input).Apply(original);
        if(value.ReviewState==ReferenceReviewState.Reviewed)Validate(FuturesReferenceQualification.Errors(value));
        return value;
    }
    public static FuturesOptionContractReadModel Option(ContractEditorIntent input,FuturesOptionContractReadModel original)
    {
        if(original.SchemaVersion!=1)throw new ArgumentException("Import a provider definition before reviewing this legacy contract");
        var value=Review(input).Apply(original);
        if(value.ReviewState==ReferenceReviewState.Reviewed)Validate(FuturesReferenceQualification.Errors(value));
        return value;
    }
    static void Validate(IReadOnlyList<string> errors){if(errors.Count>0)throw new ArgumentException("Reference is not qualified: "+string.Join(", ",errors));}
    static string Field(ContractEditorIntent input,string key)=>input.Fields.GetValueOrDefault(key)?.Trim()??"";
    static T Choice<T>(ContractEditorIntent input,string key) where T:struct,Enum => Enum.TryParse<T>(Field(input,key),false,out var value)&&Enum.IsDefined(value)?value:throw new ArgumentException("Select a valid "+key);
    static DateTimeOffset? Instant(ContractEditorIntent input,string key)
    {
        var text=Field(input,key);if(text.Length==0)return null;
        if(!DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.None,out var value)||value.Offset!=TimeSpan.Zero)throw new ArgumentException("Enter "+key+" as a UTC timestamp");
        return value;
    }
    static ReferenceConventionEditor Review(ContractEditorIntent input)
    {
        var zone=Field(input,"exchangeTimeZoneId");if(zone.Length>0)_=TimeZoneInfo.FindSystemTimeZoneById(zone);
        return new(){ReviewState=Choice<ReferenceReviewState>(input,"reviewState"),MappingVersion=Field(input,"mappingVersion"),EvidenceId=Field(input,"evidenceId"),EffectiveFromUtc=Instant(input,"effectiveFromUtc"),EffectiveUntilUtc=Instant(input,"effectiveUntilUtc"),ExchangeTimeZoneId=zone,CalendarVersion=Field(input,"calendarVersion"),LastTradingUtc=Instant(input,"lastTradingUtc"),ExerciseCutoffUtc=Instant(input,"exerciseCutoffUtc"),ExerciseStyle=Choice<ReferenceExerciseStyle>(input,"exerciseStyle"),SettlementStyle=Choice<ReferenceSettlementStyle>(input,"settlementStyle"),PremiumStyle=Choice<ReferencePremiumStyle>(input,"premiumStyle"),PremiumTickRule=Choice<ReferencePremiumTickRule>(input,"premiumTickRule"),TickRuleVersion=Field(input,"tickRuleVersion"),DayCount=Choice<ReferenceDayCount>(input,"dayCount"),UnderlyingContractId=Field(input,"underlyingContractId"),ExerciseResultContractId=Field(input,"exerciseResultContractId")};
    }
}
