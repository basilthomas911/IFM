using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

[MessagePackObject]
public sealed record GetFuturesOptionContractsPageParameter : IActorEntityId, IQueryParameter
{
    public const int DefaultPageSize = 200;
    public const int MaximumPageSize = 1000;
    [Key(0)] public string Symbol { get; init; } = string.Empty;
    [Key(1)] public int PageSize { get; init; } = DefaultPageSize;
    [Key(2)] public string? ContinuationToken { get; init; }
    [IgnoreMember] public string QueryParams =>
        $"symbol={Uri.EscapeDataString(Symbol)}&pageSize={PageSize}&continuationToken={Uri.EscapeDataString(ContinuationToken ?? string.Empty)}";

    public GetFuturesOptionContractsPageParameter() { }
    [SerializationConstructor]
    public GetFuturesOptionContractsPageParameter(string symbol, int pageSize = DefaultPageSize, string? continuationToken = null)
        => (Symbol, PageSize, ContinuationToken) = (symbol, pageSize, continuationToken);
    public string Format() => Symbol;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Symbol);
        if (PageSize is < 1 or > MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(PageSize), $"Page size must be between 1 and {MaximumPageSize}.");
        if (ContinuationToken?.Length > 16384)
            throw new ArgumentException("Invalid contract continuation token.", nameof(ContinuationToken));
    }
}
