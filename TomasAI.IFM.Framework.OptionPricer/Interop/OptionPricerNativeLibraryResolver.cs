using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.OptionPricer.Interop;

internal static class OptionPricerNativeLibraryResolver
{
    internal const string LibraryName = "ifm_option_pricer_native";

#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize() => NativeLibrary.SetDllImportResolver(
        typeof(OptionPricerNativeLibraryResolver).Assembly,
        Resolve);
#pragma warning restore CA2255

    internal static string GetRuntimeIdentifier()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Rust option pricer currently supports Windows only.");
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                $"The Rust option pricer supports x64 only, not {RuntimeInformation.ProcessArchitecture}.");
        }
        return "win-x64";
    }

    internal static string GetExpectedPath(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        return Path.GetFullPath(Path.Combine(
            baseDirectory,
            "runtimes",
            GetRuntimeIdentifier(),
            "native",
            LibraryName + ".dll"));
    }

    private static nint Resolve(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
            return 0;

        var path = GetExpectedPath(AppContext.BaseDirectory);
        if (!File.Exists(path))
        {
            throw new DllNotFoundException(
                $"The Rust option-pricer library was not found in the expected RID directory: '{path}'.");
        }
        return NativeLibrary.Load(path, assembly, DllImportSearchPath.SafeDirectories);
    }
}
