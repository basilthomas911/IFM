using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

[MessagePackObject(AllowPrivate = true)]
public sealed record FundCompositionReservationResult
{
    [Key(0)] public FundOrderProjectionReadModel Order { get; init; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(1)] public FundOrderTradeProjectionReadModel[] Trades { get; init; } = [];
    [Key(2)] public long AggregateVersion { get; init; }
    [Key(3)] public DateTime CommittedOnUtc { get; init; }
    [Key(4)] public ReservationDisposition Disposition { get; init; }
    [Key(5)] public string CanonicalRequestSha256 { get; init; } = string.Empty;
}
