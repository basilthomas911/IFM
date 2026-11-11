using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

/// <summary>Binds the persisted sequence name as a provider-neutral text value.</summary>
/// <param name="sequenceName">The persisted sequence name.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetNextSequenceId(string sequenceName) : IBindValue
{
    /// <summary>Creates the ordered, provider-neutral SQL parameters.</summary>
    /// <returns>One text parameter containing the sequence name.</returns>
    public object Bind() => Values(Text(sequenceName));
}
