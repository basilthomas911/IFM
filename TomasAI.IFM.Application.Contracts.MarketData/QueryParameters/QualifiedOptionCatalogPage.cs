namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

/// <summary>A caller-validated selection for one page of the option catalog.</summary>
public sealed record QualifiedOptionCatalogPage
{
    public QualifiedOptionCatalogPage(GetFuturesOptionContractsPageParameter request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        Symbol = request.Symbol;
        PageSize = request.PageSize;
        ContinuationToken = request.ContinuationToken;
    }

    public string Symbol { get; }
    public int PageSize { get; }
    public string? ContinuationToken { get; }
}
