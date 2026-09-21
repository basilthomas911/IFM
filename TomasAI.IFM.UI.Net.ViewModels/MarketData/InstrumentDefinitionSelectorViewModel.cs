using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>One bounded page, cancellation and response fencing; no market-data subscription.</summary>
public sealed class InstrumentDefinitionSelectorViewModel(MarketDataQueryService service) : IDisposable
{
    CancellationTokenSource? pending;
    long revision;
    bool disposed;
    public InstrumentDefinitionPage? Page { get; private set; }

    public async Task<bool> SearchAsync(InstrumentDefinitionPageRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var version = ++revision;
        pending?.Cancel();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        pending = lifetime;
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            request.Validate();
            var result = await service.GetInstrumentDefinitionsAsync(request, lifetime.Token);
            if (disposed || version != revision) return false;
            lifetime.Token.ThrowIfCancellationRequested();
            if (!result.Success || result.Value is null)
                throw new UiServiceOperationException(result.ErrorCode, result.ErrorMessage ?? "Definition search failed.");
            if (result.Value.Items.Length > request.PageSize)
                throw new InvalidDataException("Definition result exceeds the requested page size.");
            Page = result.Value;
            return true;
        }
        catch (OperationCanceledException) when (disposed || version != revision) { return false; }
        finally { if (ReferenceEquals(pending, lifetime)) pending = null; }
    }

    public async Task<FuturesContractV3ReadModel> ResolveUnderlyingAsync(string id, CancellationToken token)
    {
        var result = await service.ResolveUnderlyingAsync(id, token);
        token.ThrowIfCancellationRequested();
        if (!result.Success || result.Value is null)
            throw new UiServiceOperationException(result.ErrorCode, result.ErrorMessage ?? "Underlying future not found.");
        return result.Value;
    }
    public void Dispose()
    {
        disposed = true;
        ++revision;
        pending?.Cancel();
        Page = null;
    }
}
