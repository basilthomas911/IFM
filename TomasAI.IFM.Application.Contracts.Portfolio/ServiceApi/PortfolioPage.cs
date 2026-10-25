using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioPage<T>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(0)] public T[] Items { get; init; } = [];
    [Key(1)] public string? NextPageToken { get; init; }
    [Key(2)] public int PageSize { get; init; }
}
