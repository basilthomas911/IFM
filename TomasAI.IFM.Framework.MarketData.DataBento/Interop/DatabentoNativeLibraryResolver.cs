using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento.Interop;

internal static class DatabentoNativeLibraryResolver
{
    internal const string LibraryName = "databento_feed_native";

#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize() => NativeLibrary.SetDllImportResolver(
        typeof(DatabentoNativeLibraryResolver).Assembly,
        Resolve);
#pragma warning restore CA2255

    internal static string GetRuntimeIdentifier()
    {
        var architecture = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? "x64"
            : throw new PlatformNotSupportedException(
                $"Databento native runtime supports x64 only, not {RuntimeInformation.ProcessArchitecture}.");
        var operatingSystem = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsLinux()
                ? "linux"
                : throw new PlatformNotSupportedException(
                    "Databento native runtime supports Windows and Linux only.");
        return $"{operatingSystem}-{architecture}";
    }

    internal static string GetExpectedPath(string baseDirectory, string runtimeIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        if (runtimeIdentifier.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || runtimeIdentifier.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("The runtime identifier is invalid.", nameof(runtimeIdentifier));
        }
        var fileName = runtimeIdentifier.StartsWith("win-", StringComparison.Ordinal)
            ? LibraryName + ".dll"
            : runtimeIdentifier.StartsWith("linux-", StringComparison.Ordinal)
                ? "lib" + LibraryName + ".so"
                : throw new PlatformNotSupportedException(
                    $"Databento native runtime '{runtimeIdentifier}' is not supported.");
        return Path.GetFullPath(Path.Combine(
            baseDirectory,
            "runtimes",
            runtimeIdentifier,
            "native",
            fileName));
    }

    private static nint Resolve(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return 0;
        }
        var path = GetExpectedPath(AppContext.BaseDirectory, GetRuntimeIdentifier());
        if (!File.Exists(path))
        {
            throw new DllNotFoundException(
                $"The Databento native library was not found in the expected RID directory: '{path}'.");
        }
        return NativeLibrary.Load(path, assembly, DllImportSearchPath.SafeDirectories);
    }
}
