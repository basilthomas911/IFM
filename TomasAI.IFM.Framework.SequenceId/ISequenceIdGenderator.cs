
namespace TomasAI.IFM.Framework.SequenceId
{
    public interface ISequenceIdGenerator
    {
        Task<long> GetSequenceIdAsync(SequenceName sequenceIdType);
    }
}
