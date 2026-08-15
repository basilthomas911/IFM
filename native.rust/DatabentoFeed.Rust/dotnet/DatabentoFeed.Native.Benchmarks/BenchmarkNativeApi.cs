using DatabentoFeed.Native.Interop;

internal static class BenchmarkNativeApi
{
    internal static NativeApi Load(string implementation)
    {
        string variable = implementation == "Cpp" ? "DBF_CPP_DLL" : "DBF_RUST_DLL";
        string? path = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException($"Set {variable}.");
        return new(path);
    }
}
