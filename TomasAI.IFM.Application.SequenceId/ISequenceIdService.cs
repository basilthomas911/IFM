using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.SequenceId
{
    public interface ISequenceIdService
    {
        Task<long> GetNextSequenceIdAsync(SequenceName sequenceIdType);
    }
}
