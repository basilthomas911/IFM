using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

/// <summary>Persists before dispatch, reconciles before retry, and distinguishes a committed receipt from queue acceptance.</summary>
public sealed class FinancialPostingOperation(IPortfolioFinancialApi api,IPendingFinancialOperationStore store)
{
    readonly SemaphoreSlim _gate=new(1,1);
    public async Task<PendingFinancialOperation> SubmitAsync(PostFundTransactionCommand request,CancellationToken token=default)
    {
        await store.SaveAsync(new(request,PendingFinancialPhase.Prepared,"Prepared; awaiting committed receipt."),token);
        return await RecoverAsync(request.OperationId,token);
    }
    public async Task<PendingFinancialOperation> RecoverAsync(Guid operationId,CancellationToken token=default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var pending=await store.LoadAsync(operationId,token) ?? throw new KeyNotFoundException("Pending financial operation was not found.");
            if(pending.Phase is PendingFinancialPhase.Committed or PendingFinancialPhase.ExpiredWithoutPosting) return pending;
            try
            {
                var reconciled=await QueryAsync(pending,token);
                if(reconciled is not null) return reconciled;
                pending=await store.SaveAsync(pending with { Phase=PendingFinancialPhase.OutcomeUnknown,
                    Message="Awaiting committed financial outcome. This operation will retain its identity." },token);
                var response=await api.PostAsync(pending.Request,token);
                // A successful Command response is still not the correlated financial receipt.
                for(var attempt=0;attempt<10;attempt++)
                {
                    reconciled=await QueryAsync(pending,token);
                    if(reconciled is not null) return reconciled;
                    if(!response.Success) break;
                    await Task.Delay(100,token);
                }
                return await store.SaveAsync(pending with { Message=response.Success
                    ? "Outcome unknown; check this original operation again."
                    : $"Outcome not confirmed: {response.ErrorMessage}. Check the original operation." },token);
            }
            catch(Exception error) when(error is not InvalidDataException)
            {
                // An interrupted request can already have committed. Do not label it cancelled or create a replacement.
                return await store.SaveAsync(pending with { Phase=PendingFinancialPhase.OutcomeUnknown,
                    Message="Outcome unknown: "+error.Message },CancellationToken.None);
            }
        }
        finally { _gate.Release(); }
    }
    async Task<PendingFinancialOperation?> QueryAsync(PendingFinancialOperation pending,CancellationToken token)
    {
        var request=pending.Request;
        var scope=new FinancialReadScope { PortfolioId=request.PortfolioId,FundId=request.Body.FundId,Access=request.Access };
        var response=await api.GetPostingReceiptAsync(scope,new(request.OperationId),token);
        if(!response.Success || response.Value is null || response.Value.Status is not (FinancialReadStatus.Found or FinancialReadStatus.NotFound))
            throw new IOException("Financial receipt service is unavailable.");
        if(response.Value.Status==FinancialReadStatus.Found && response.Value.Value?.Posting is { } completed)
        {
            if(completed.OperationId!=request.OperationId || completed.InputHash!=request.InputSha256 ||
                completed.Receipt.FundId!=request.Body.FundId || completed.Receipt.BookId!=request.Body.BookId)
                throw new InvalidDataException("Returned receipt does not match the original financial request.");
            return await store.SaveAsync(pending with { Phase=PendingFinancialPhase.Committed,Completion=completed,
                Message="Committed. History may still be updating." },token);
        }
        if(response.Value.Status==FinancialReadStatus.Found || response.Value.Value is not null)
            throw new InvalidDataException("Financial receipt response does not contain the expected posting outcome.");
        // This authoritative read acquires the financial fence. All new posting attempts check expiry after that same fence.
        if(response.Value.Status==FinancialReadStatus.NotFound && response.Value.ObservedAtUtc>=request.ExpiresAtUtc)
            return await store.SaveAsync(pending with { Phase=PendingFinancialPhase.ExpiredWithoutPosting,
                Message="The request expired; the authoritative receipt query confirms no posting." },token);
        return null;
    }
}
