using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

internal readonly record struct GetNextSequenceId(string sequenceName) : IBindValue
{
    public object Bind() => Values(Text(sequenceName));
}
