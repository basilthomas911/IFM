using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

[MessagePackObject]
public sealed record GetEconomicCalendarPageParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public EconomicCalendarPageRequest Request { get; init; } = new();
    [IgnoreMember] public string? QueryParams { get; private set; }

    public GetEconomicCalendarPageParameter() { }

    [SerializationConstructor]
    public GetEconomicCalendarPageParameter(EconomicCalendarPageRequest request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        var countries = Uri.EscapeDataString(string.Join(',', request.CountryCodes));
        QueryParams = $"startDateUtc={Uri.EscapeDataString(request.StartDateUtc.ToString("O"))}" +
            $"&endDateUtc={Uri.EscapeDataString(request.EndDateUtc.ToString("O"))}" +
            $"&countryCodes={countries}&pageSize={request.PageSize}" +
            (string.IsNullOrEmpty(request.ContinuationToken)
                ? string.Empty
                : $"&continuationToken={Uri.EscapeDataString(request.ContinuationToken)}");
    }

    public string Format()
    {
        var countries = string.Join(',', Request.CountryCodes.Order(StringComparer.OrdinalIgnoreCase));
        var identity = $"{Request.StartDateUtc.Ticks}|{Request.EndDateUtc.Ticks}|{countries}|{Request.PageSize}|{Request.ContinuationToken}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
}
