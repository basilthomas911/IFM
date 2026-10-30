using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetNextSequenceId(string sequenceName) : IBindValue
{
    public object Bind() => Values(Text(sequenceName));
}
