namespace TomasAI.IFM.Framework.SequenceId;

public interface ISequenceIdDbContext
{
    Task<long> GetNextSequenceIdAsync(SequenceName sequenceIdType);
}

