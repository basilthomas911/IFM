using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

[MessagePackObject]
public sealed record WorkflowCompositionHandoffState
{
    [Key(0)] public CompositionHandoffStatus Status { get; init; }
    [Key(1)] public Guid SelectionSourceEventId { get; init; }
    [Key(2)] public long AcceptedSelectionRevision { get; init; }
    ReserveFundOrderCompositionRequest _request;
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    [Key(3)] public ReserveFundOrderCompositionRequest Request { get => Copy(_request); init => _request = Copy(value); }
    FundCompositionReservationResult? _reservation;
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    [Key(4)] public FundCompositionReservationResult? Reservation { get => Copy(_reservation); init => _reservation = Copy(value); }
    static ReserveFundOrderCompositionRequest Copy(ReserveFundOrderCompositionRequest value) => value?.DefensiveCopy()!;
    static FundCompositionReservationResult? Copy(FundCompositionReservationResult? value) => value is null ? null : value with { Trades = [.. value.Trades] };
    [Key(5)] public string ReservationRequestSha256 { get; init; } = string.Empty;
    [Key(6)] public DateTime UpdatedAtUtc { get; init; }
}
