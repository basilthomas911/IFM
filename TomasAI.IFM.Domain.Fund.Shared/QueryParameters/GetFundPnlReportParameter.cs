using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the fund PnL report for a specific fund over a date range.
/// </summary>
[MessagePackObject(false)]
public record GetFundPnlReportParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int FundId { get; init; }
    [Key(1)] public DateOnly StartDate { get; init; }
    [Key(2)] public DateOnly EndDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFundPnlReportParameter() { }

    [SerializationConstructor]
    public GetFundPnlReportParameter(int fundId, DateOnly startDate, DateOnly endDate)
    {
        FundId = fundId;
        StartDate = startDate;
        EndDate = endDate;
        QueryParams = $"fundId={FundId}&startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{FundId}.{StartDate:yyyy-MM-dd}.{EndDate:yyyy-MM-dd}";
}
