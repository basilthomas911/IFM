using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Framework.OptionPricer.Interop;

internal enum OptionPricerImplementation
{
    Managed,
    Rust
}

internal static class OptionPricerBackend
{
    internal const string AppContextKey = "TomasAI.IFM.OptionPricer.Implementation";
    internal const string EnvironmentVariable = "IFM_OPTION_PRICER_IMPLEMENTATION";

    private static readonly OptionPricerImplementation SelectedImplementation = ResolveConfiguredImplementation();

    static OptionPricerBackend()
    {
        if (SelectedImplementation == OptionPricerImplementation.Rust)
            RustOptionModel.EnsureAvailable();
    }

    internal static OptionPricerImplementation Implementation => SelectedImplementation;

    internal static bool UseRust
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => SelectedImplementation == OptionPricerImplementation.Rust;
    }

    internal static OptionPricerImplementation Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, nameof(OptionPricerImplementation.Managed), StringComparison.OrdinalIgnoreCase))
        {
            return OptionPricerImplementation.Managed;
        }
        if (string.Equals(value, nameof(OptionPricerImplementation.Rust), StringComparison.OrdinalIgnoreCase))
            return OptionPricerImplementation.Rust;

        throw new InvalidOperationException(
            $"Unknown option-pricer implementation '{value}'. Expected Managed or Rust.");
    }

    private static OptionPricerImplementation ResolveConfiguredImplementation()
    {
        var appContextValue = AppContext.GetData(AppContextKey);
        if (appContextValue is not null && appContextValue is not string)
        {
            throw new InvalidOperationException(
                $"AppContext value '{AppContextKey}' must be a string when configured.");
        }

        var value = appContextValue as string;
        if (string.IsNullOrWhiteSpace(value))
            value = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return Parse(value);
    }
}
