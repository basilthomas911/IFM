using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Allocates outside the financial transaction. Gaps are valid and no identity is reused after failure.</summary>
public sealed class FinancialIdentityAllocator(ISequenceIdGenerator sequences)
{
    public async ValueTask<int> BookAsync(CancellationToken token = default) => checked((int)await Next(SequenceName.PortfolioLedger_BookId,token));
    public async ValueTask<int> AccountAsync(CancellationToken token = default) => checked((int)await Next(SequenceName.PortfolioLedger_AccountId,token));
    public ValueTask<long> JournalAsync(CancellationToken token = default) => Next(SequenceName.PortfolioLedger_JournalId,token);
    public ValueTask<long> TransactionAsync(CancellationToken token = default) => Next(SequenceName.PortfolioLedger_TransactionId,token);
    async ValueTask<long> Next(SequenceName name,CancellationToken token)
    {
        var value=await sequences.GetSequenceIdAsync(name,token).ConfigureAwait(false);
        return value>0?value:throw new InvalidOperationException("Sequence returned an invalid financial business identity.");
    }
}
