using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class DatabentoDiagnosticsTests
{
    [Fact]
    public unsafe void Unknown_diagnostic_kind_cannot_be_treated_as_an_advisory_warning()
    {
        var before = DatabentoDiagnostics.ErrorCount;
        DatabentoProviderDiagnostic? captured = null;
        try
        {
            DatabentoDiagnostics.Install(value => captured = value);
            delegate* unmanaged[Cdecl]<int, int, int, int, byte*, uint, void> callback = &DatabentoDiagnostics.Receive;
            var text = Encoding.UTF8.GetBytes("future driver diagnostic");
            fixed (byte* pointer = text) callback(99, 123, 0, 1, pointer, (uint)text.Length);
            Assert.Equal("UNKNOWN_DIAGNOSTIC_KIND", captured!.CodeName);
            Assert.True(DatabentoDiagnostics.ErrorCount > before);
        }
        finally { DatabentoDiagnostics.Install(_ => { }); }
    }

    [Fact]
    public unsafe void Callback_preserves_code_and_text_but_redacts_credentials()
    {
        var previous = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        DatabentoProviderDiagnostic? captured = null;
        try
        {
            Environment.SetEnvironmentVariable("DATABENTO_API_KEY", "diagnostic-test-secret");
            DatabentoDiagnostics.Install(value => captured = value);
            delegate* unmanaged[Cdecl]<int, int, int, int, byte*, uint, void> callback = &DatabentoDiagnostics.Receive;
            var text = Encoding.UTF8.GetBytes("Original provider detail diagnostic-test-secret");
            fixed (byte* pointer = text) callback(1, 8, 1, 1, pointer, (uint)text.Length);
            Assert.NotNull(captured);
            Assert.Equal(8, captured.Code); Assert.Equal("REPLAY_DATA_AGED_OUT", captured.CodeName);
            Assert.True(captured.SessionFatal); Assert.True(captured.IsLast);
            Assert.Equal("Original provider detail [redacted]", captured.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABENTO_API_KEY", previous);
            DatabentoDiagnostics.Install(_ => { });
        }
    }

    [Fact]
    public unsafe void Logger_exception_cannot_escape_reverse_PInvoke_and_marks_diagnostics_unhealthy()
    {
        var before = DatabentoDiagnostics.ErrorCount;
        try
        {
            DatabentoDiagnostics.Install(_ => throw new InvalidOperationException("sink failed"));
            delegate* unmanaged[Cdecl]<int, int, int, int, byte*, uint, void> callback = &DatabentoDiagnostics.Receive;
            var text = Encoding.UTF8.GetBytes("slow reader warning");
            fixed (byte* pointer = text) callback(2, 2, 0, 1, pointer, (uint)text.Length);
            Assert.True(DatabentoDiagnostics.ErrorCount > before);
            Assert.Contains("diagnostic sink failed", DatabentoDiagnostics.LastFailure);
        }
        finally { DatabentoDiagnostics.Install(_ => { }); }
    }
}
