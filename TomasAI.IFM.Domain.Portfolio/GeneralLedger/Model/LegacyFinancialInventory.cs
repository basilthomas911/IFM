using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Streams canonical legacy data into a resumable dry-run manifest. Missing financial evidence remains quarantined.</summary>
public sealed class LegacyFinancialInventory(IFundDbReadContext source,LegacyFinancialInventoryStore destination)
{
    public async Task<LegacyFinancialInventoryResult> RunAsync(LegacyFinancialInventoryScope scope,CancellationToken token)
    {
        await destination.BeginAsync(scope,token);
        var mode=Enum.Parse<LedgerImportMode>(scope.ImportMode);
        long count=0;
        await foreach(var row in source.StreamCanonicalFundTransactionsAsync(scope.SourceFundId,scope.Start,scope.End,token).WithCancellation(token))
        {
            if(row.FundId!=scope.SourceFundId || row.ValueDate<scope.Start || row.ValueDate>scope.End)
                throw new InvalidDataException("Canonical source returned a row outside the requested inventory scope.");
            // The legacy DTO has no currency, confirmed movement, absolute-valuation or correction evidence.
            // Do not invent those facts from the destination book, its opening capital or old balance columns.
            var classified=LegacyFinancialClassification.Classify(row,new("",false,false,false,null,$"LegacyFund:{row.FundId}"),mode);
            var key=FinancialCanonicalHash.Compute(new { row.FundId,row.ValueDate,row.OrderId,row.TradeId,row.TradeType,row.TransactionType,row.TransactionDate });
            await destination.SaveRowAsync(scope.InventoryId,new(key,classified.SourceHash,JsonConvert.SerializeObject(row),
                classified.Disposition.ToString(),classified.Reason,row.Amount),token);
            count=checked(count+1);
        }
        return await destination.CompleteAsync(scope.InventoryId,count,token);
    }
}
