using System.Text.Json;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

public enum PendingFinancialPhase { Prepared=0, OutcomeUnknown=1, Committed=2, ExpiredWithoutPosting=3 }
public sealed record PendingFinancialOperation(PostFundTransactionCommand Request,PendingFinancialPhase Phase,
    string Message,LedgerPostingCompletedEvent? Completion=null);

public interface IPendingFinancialOperationStore
{
    Task<PendingFinancialOperation?> LoadAsync(Guid operationId,CancellationToken token=default);
    Task<PendingFinancialOperation> SaveAsync(PendingFinancialOperation operation,CancellationToken token=default);
    Task<IReadOnlyList<PendingFinancialOperation>> ListAsync(int portfolioId,int fundId,CancellationToken token=default);
}

/// <summary>Atomic per-user journal of exact financial requests. Closing a window or restarting the UI never replaces an uncertain OperationId.</summary>
public sealed class PendingFinancialOperationStore(string directory):IPendingFinancialOperationStore
{
    readonly string _directory=Path.GetFullPath(directory);
    public static PendingFinancialOperationStore ForCurrentUser()=>new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IFM","Portfolio","PendingFinancialOperations"));

    public async Task<PendingFinancialOperation?> LoadAsync(Guid operationId,CancellationToken token=default)
    {
        var path=FilePath(operationId);
        if(!File.Exists(path)) return null;
        if(new FileInfo(path).Length>2*1024*1024) throw new InvalidDataException("Pending financial request exceeds its bound.");
        var result=JsonSerializer.Deserialize<PendingFinancialOperation>(await File.ReadAllTextAsync(path,token))
            ??throw new InvalidDataException("Pending financial request is unreadable.");
        Validate(result);
        if(result.Request.OperationId!=operationId) throw new InvalidDataException("Pending operation file identity differs from its request.");
        return result;
    }
    public async Task<PendingFinancialOperation> SaveAsync(PendingFinancialOperation operation,CancellationToken token=default)
    {
        Validate(operation); Directory.CreateDirectory(_directory);
        var path=FilePath(operation.Request.OperationId);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await using var held=await LockAsync(path+".lock",timeout.Token);
        var prior=await LoadAsync(operation.Request.OperationId,token);
        if(prior is not null && prior.Request.InputSha256!=operation.Request.InputSha256)
            throw new InvalidOperationException("OperationId already belongs to another financial request.");
        if(prior?.Phase is PendingFinancialPhase.Committed or PendingFinancialPhase.ExpiredWithoutPosting) return prior;
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        var bytes=JsonSerializer.SerializeToUtf8Bytes(operation);
        if(bytes.Length>2*1024*1024) throw new InvalidDataException("Pending financial request exceeds its bound.");
        try
        {
            await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
            { await output.WriteAsync(bytes,token); output.Flush(flushToDisk:true); }
            File.Move(temporary,path,overwrite:true);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
        return operation;
    }
    public async Task<IReadOnlyList<PendingFinancialOperation>> ListAsync(int portfolioId,int fundId,CancellationToken token=default)
    {
        if(!Directory.Exists(_directory)) return [];
        var result=new List<PendingFinancialOperation>();
        foreach(var file in Directory.EnumerateFiles(_directory,"*.json"))
        {
            token.ThrowIfCancellationRequested();
            if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file),"N",out var id)) continue;
            var item=await LoadAsync(id,token);
            if(item?.Request.PortfolioId==portfolioId && item.Request.Body.FundId==fundId)
                result.Add(item);
        }
        return result.OrderByDescending(x=>x.Request.RequestedAtUtc).ToArray();
    }
    string FilePath(Guid operationId)=>operationId==Guid.Empty ? throw new ArgumentException("OperationId is required.")
        :Path.Combine(_directory,operationId.ToString("N")+".json");
    static void Validate(PendingFinancialOperation value)
    {
        if(value.Request.OperationId==Guid.Empty || value.Request.CommandId!=value.Request.OperationId ||
            value.Request.InputSha256!=FinancialCanonicalHash.Request(value.Request))
            throw new InvalidDataException("Pending financial request integrity failed.");
        if(value.Phase==PendingFinancialPhase.Committed && (value.Completion?.OperationId!=value.Request.OperationId ||
            value.Completion.InputHash!=value.Request.InputSha256))
            throw new InvalidDataException("Financial completion does not match the persisted request.");
    }
    static async Task<FileStream> LockAsync(string path,CancellationToken token)
    {
        while(true)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) { await Task.Delay(25,token); }
        }
    }
}
