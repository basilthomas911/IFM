using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.Identity;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

internal static class SequenceIdDbContextExtensions
{
    extension(ISequenceIdDbContext context)
    {
        /// <summary>Executes a named sequence statement and maps its single numeric result.</summary>
        internal Task<long> ExecuteSequenceAsync(string operation, string statement, SequenceName sequenceName,
            CancellationToken cancellationToken)
            => context.Use($"{nameof(SequenceIdDbSql)}.{operation}", statement)
                .SetParameters(new GetNextSequenceId(sequenceName.ToStringFast()))
                .ExecuteScalarAsync(SequenceIdDbContext.MapToSequenceId, cancellationToken);
    }
}
