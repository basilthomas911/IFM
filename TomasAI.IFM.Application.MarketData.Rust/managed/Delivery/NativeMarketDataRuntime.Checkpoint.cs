using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Rust.Interop;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>A copied immutable native owner image, not an activation or cross-owner proof.</summary>
/// <param name="Path">Absolute file path.</param>
/// <param name="Bytes">Exact complete file length.</param>
/// <param name="Sha256">SHA256 of the complete file including the native header.</param>
public sealed record NativeCheckpointFile(string Path,long Bytes,string Sha256);

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Whether the loaded native library supports the complete bounded checkpoint protocol.</summary>
    public bool SupportsNativeCheckpoint=>library.CheckpointExport is not null && library.CheckpointImport is not null && library.CheckpointBegin is not null && library.CheckpointEnd is not null && library.CheckpointRestoreRoot is not null;
    /// <summary>Bounds subsequently bound staged owner resources to an explicit isolated restored root.</summary>
    public unsafe void ConfigureNativeCheckpointRestoreRoot(string scope,string existingOwnedRoot)
        =>ConfigureNativeCheckpointRestoreRoot(library,scope,existingOwnedRoot);
    /// <summary>Configures staged native ownership without constructing a runtime or provider.</summary>
    public static void ConfigureNativeCheckpointRestoreRoot(string absoluteLibraryPath,string scope,string existingOwnedRoot)
        =>ConfigureNativeCheckpointRestoreRoot(NativeMarketDataLibrary.Load(absoluteLibraryPath),scope,existingOwnedRoot);
    static unsafe void ConfigureNativeCheckpointRestoreRoot(NativeMarketDataLibrary library,string scope,string existingOwnedRoot)
    {
        var configure=library.CheckpointRestoreRoot??throw new NotSupportedException("Native checkpoint restore-root fencing is unavailable.");
        var root=ValidatedPath(existingOwnedRoot);if(!Directory.Exists(root))throw new InvalidDataException("Native checkpoint restore requires an existing isolated root.");
        var name=Encoding.UTF8.GetBytes(scope);var path=Encoding.UTF8.GetBytes(root);
        fixed(byte* namePointer=name,pathPointer=path)NativeMarketDataLibrary.Check(configure(namePointer,checked((uint)name.Length),pathPointer,checked((uint)path.Length)),"checkpoint restore-root fence");
    }
    /// <summary>Freezes native mutations under an existing caller-owned source-forwarding fence.</summary>
    public unsafe void BeginNativeCheckpoint(string scope,ulong heldLease)
    {
        var begin=library.CheckpointBegin??throw new NotSupportedException("Native checkpoint freeze is unavailable.");
        var status=SessionForwardingStatus();
        if(!status.Supported || heldLease==0 || status.Lease!=heldLease || status.ActiveSends!=0)throw new InvalidOperationException("Native checkpoint requires the exact held forwarding lease.");
        var name=Encoding.UTF8.GetBytes(scope);
        fixed(byte* pointer=name)NativeMarketDataLibrary.Check(begin(pointer,checked((uint)name.Length),heldLease),"owner checkpoint freeze");
    }
    /// <summary>Ends only the native image freeze, retaining the caller's source-forwarding fence.</summary>
    public void EndNativeCheckpoint(ulong heldLease)
        =>NativeMarketDataLibrary.Check((library.CheckpointEnd??throw new NotSupportedException("Native checkpoint freeze is unavailable."))(heldLease),"owner checkpoint unfreeze");
    /// <summary>Creates and verifies an immutable image while the native checkpoint freeze remains held.</summary>
    public unsafe NativeCheckpointFile ExportNativeCheckpoint(string scope,ulong heldLease,string newAbsolutePath)
    {
        var export=library.CheckpointExport??throw new NotSupportedException("Native owner checkpoint export is unavailable.");
        var status=SessionForwardingStatus();
        if(!status.Supported || heldLease==0 || status.Lease!=heldLease || status.ActiveSends!=0)throw new InvalidOperationException("Native checkpoint requires the exact held forwarding lease.");
        var path=ValidatedPath(newAbsolutePath);if(File.Exists(path))throw new InvalidOperationException("Native checkpoint export never overwrites a file.");
        var name=Encoding.UTF8.GetBytes(scope);var wirePath=Encoding.UTF8.GetBytes(path);var hash=new byte[32];
        fixed(byte* scopePointer=name,pathPointer=wirePath,hashPointer=hash)
            NativeMarketDataLibrary.Check(export(scopePointer,checked((uint)name.Length),heldLease,pathPointer,checked((uint)wirePath.Length),hashPointer),"owner checkpoint export");
        var evidence=VerifyNativeCheckpoint(path,hash);
        status=SessionForwardingStatus();
        if(status.Lease!=heldLease || status.ActiveSends!=0)throw new InvalidOperationException("Native forwarding fence changed during checkpoint export; partial evidence cannot certify a boundary.");
        return evidence;
    }
    /// <summary>Verifies and imports an isolated image; native code refuses active owner replacement.</summary>
    public unsafe NativeCheckpointFile ImportNativeCheckpoint(string scope,string absolutePath,string expectedFileSha256)
        =>StageNativeCheckpoint(library,scope,absolutePath,expectedFileSha256);
    /// <summary>Uses the actual native import codec to validate and stage an isolated image without creating a runtime or provider.</summary>
    public static NativeCheckpointFile StageNativeCheckpoint(string absoluteLibraryPath,string scope,string absolutePath,string expectedFileSha256)
        =>StageNativeCheckpoint(NativeMarketDataLibrary.Load(absoluteLibraryPath),scope,absolutePath,expectedFileSha256);
    static unsafe NativeCheckpointFile StageNativeCheckpoint(NativeMarketDataLibrary library,string scope,string absolutePath,string expectedFileSha256)
    {
        var import=library.CheckpointImport??throw new NotSupportedException("Native owner checkpoint import is unavailable.");
        var path=ValidatedPath(absolutePath);var hash=Convert.FromHexString(expectedFileSha256);
        if(hash.Length!=32)throw new ArgumentException("A full-file SHA256 is required.",nameof(expectedFileSha256));
        var evidence=VerifyNativeCheckpoint(path,hash);var name=Encoding.UTF8.GetBytes(scope);var wirePath=Encoding.UTF8.GetBytes(path);
        fixed(byte* scopePointer=name,pathPointer=wirePath,hashPointer=hash)
            NativeMarketDataLibrary.Check(import(scopePointer,checked((uint)name.Length),pathPointer,checked((uint)wirePath.Length),hashPointer),"owner checkpoint import");
        return evidence;
    }
    static string ValidatedPath(string path)
    {
        if(!Path.IsPathFullyQualified(path) || path.IndexOf('\0')>=0)throw new ArgumentException("Native checkpoint requires an absolute local path.",nameof(path));
        return Path.GetFullPath(path);
    }
    static NativeCheckpointFile VerifyNativeCheckpoint(string path,ReadOnlySpan<byte> expected)
    {
        using var file=File.OpenRead(path);
        if(file.Length is <48 or >64*1024*1024+48)throw new InvalidDataException("Native checkpoint exceeds its format bounds.");
        var actual=SHA256.HashData(file);
        if(!CryptographicOperations.FixedTimeEquals(actual,expected))throw new InvalidDataException("Native checkpoint file hash differs from its native receipt.");
        return new(path,file.Length,Convert.ToHexString(actual).ToLowerInvariant());
    }
}
