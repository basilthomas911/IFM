using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.SequenceId
{
    public class SequenceIdService : ISequenceIdService
    {
        ISequenceIdDbContext _sequenceIdDb;

        public SequenceIdService(ISequenceIdDbContext sequenceIdDb) 
        {
            _sequenceIdDb = sequenceIdDb;
        }

        /// <summary>
        /// Get next sequence id    
        /// </summary>
        /// <param name="sequenceIdType"></param>
        /// <returns></returns>
        public async  Task<long> GetNextSequenceIdAsync(SequenceName sequenceIdType)
            => await _sequenceIdDb.GetNextSequenceIdAsync(sequenceIdType);
    }
}
