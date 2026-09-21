using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;

/// <summary>Identifies one bounded prior-day Daily Bollinger history query.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFuturesBollingerBandHistoryParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string RootSymbol { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public int MaxDays { get; init; }
    [IgnoreMember] public string? QueryParams { get; private set; }

    public GetFuturesBollingerBandHistoryParameter() { }

    [SerializationConstructor]
    public GetFuturesBollingerBandHistoryParameter(string rootSymbol, DateOnly valueDate, int maxDays)
    {
        RootSymbol = rootSymbol ?? string.Empty;
        ValueDate = valueDate;
        MaxDays = maxDays;
        QueryParams = $"rootSymbol={RootSymbol}&valueDate={ValueDate:yyyy-MM-dd}&maxDays={MaxDays}";
    }

    /// <summary>Formats a stable actor thread identity for the query.</summary>
    public string Format() => $"{RootSymbol}.{ValueDate:yyyyMMdd}.{MaxDays}";
}
