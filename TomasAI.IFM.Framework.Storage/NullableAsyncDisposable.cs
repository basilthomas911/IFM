using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>Preserves optional resource ownership while allowing context-free async disposal.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct NullableAsyncDisposable(IAsyncDisposable? resource) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => resource?.DisposeAsync() ?? ValueTask.CompletedTask;
}
