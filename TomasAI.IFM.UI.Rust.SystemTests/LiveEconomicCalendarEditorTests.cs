using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public unsafe class LiveEconomicCalendarEditorTests
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    [Fact]
    public void Calendar_load_filter_and_disposable_entry_roundtrip_use_backend_completion()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        Assert.True(http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode);
        var old = Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");
        Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE", "live");
        delegate* unmanaged[Cdecl]<uint, ApiV1*, uint, int> get = &BridgeBootstrap.GetApi;
        ApiV4 api; Assert.Equal(0, get(4, (ApiV1*)&api, 48));
        ulong handle; Assert.Equal(0, api.Base.Create(&handle));
        string? disposableId = null;
        ContractEditorPublication? state = null;
        try
        {
            state = Run(api, handle, new(1, Guid.NewGuid(), "economic-calendar", "load", 0, null, []));
            Assert.NotEmpty(state.Result.Choices["countryCode"]);
            state = Run(api, handle, new(1, Guid.NewGuid(), "economic-calendar", "range", state.Generation, null, new() { ["filterDate"] = "2026-10-13", ["filterCountry"] = "US" }));
            foreach(var previousName in new[]{"Rust UI qualification cd6876cb97c049049ab8f68c540d1199", "Rust UI qualification 8470061e2a1f42d7ac4b1468c97e255e", "Rust UI qualification 591a5830315843d08289916c51d53d99"})
            {
                var previousDisposable = state.Result.Rows.SingleOrDefault(row => row.Fields["eventName"] == previousName);
                if (previousDisposable is not null)
                    state = Run(api, handle, new(1, Guid.NewGuid(), "economic-calendar", "remove", state.Generation, previousDisposable.Id, []));
            }
            var country = state.Result.Context!["filterCountry"];
            var instant = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            var name = "Rust UI qualification " + Guid.NewGuid().ToString("N");
            var fields = new Dictionary<string, string> { ["eventDate"] = instant, ["countryCode"] = country, ["eventName"] = name, ["actual"] = "1.25", ["forecast"] = "1.20", ["prior"] = "1.10", ["impact"] = "Low", ["unit"] = "%", ["change"] = "0.15", ["changePercentage"] = "13.64" };
            state = Run(api, handle, new(1, Guid.NewGuid(), "economic-calendar", "add", state.Generation, null, fields));
            var added = Assert.Single(state.Result.Rows, row => row.Fields["eventName"] == name);
            disposableId = added.Id;
            Assert.Equal("1.25", added.Fields["actual"]);
            Assert.Equal(12, added.Fields.Count);
            fields["actual"] = "1.30";
            state = Run(api, handle, new(1, Guid.NewGuid(), "economic-calendar", "change", state.Generation, disposableId, fields));
            Assert.Equal("1.30", Assert.Single(state.Result.Rows, row => row.Id == disposableId).Fields["actual"]);
            state = Run(api, handle, new(1, Guid.NewGuid(), "economic-calendar", "range", state.Generation, null, new() { ["filterDate"] = state.Result.Context!["filterDate"], ["filterCountry"] = country }));
            Assert.Contains(state.Result.Rows, row => row.Id == disposableId);
            state = Run(api, handle, new(1, Guid.NewGuid(), "economic-calendar", "remove", state.Generation, disposableId, []));
            Assert.DoesNotContain(state.Result.Rows, row => row.Id == disposableId);
            disposableId = null;
            var dir = Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS") ?? ".artifacts/rust-ui/tests";
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "live-calendar-qualification.json"), JsonSerializer.Serialize(new { state.Result.Context, countries = state.Result.Choices["countryCode"].Length, state.Outcomes, disposableEntryRemoved = true }, Json));
        }
        finally
        {
            if (disposableId is not null && state is not null)
            {
                var refreshed = Run(api, handle, new(1, Guid.NewGuid(), "economic-calendar", "load", 0, null, new() { ["filterDate"] = state.Result.Context!["filterDate"], ["filterCountry"] = state.Result.Context["filterCountry"] }));
                if (refreshed.Result.Rows.Any(row => row.Id == disposableId))
                    Run(api, handle, new(1, Guid.NewGuid(), "economic-calendar", "remove", refreshed.Generation, disposableId, []));
            }
            Assert.Equal(0, api.Base.Destroy(handle));
            Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE", old);
        }
    }
    static ContractEditorPublication Run(ApiV4 api, ulong handle, ContractEditorIntent intent)
    {
        var bytes = BridgeWire.Serialize(intent);
        fixed (byte* p = bytes) Assert.Equal(0, api.Submit(handle, p, bytes.Length));
        var deadline = DateTime.UtcNow.AddSeconds(45);
        do
        {
            int n; api.Base.Copy(handle, 5, null, 0, &n);
            var snapshot = new byte[n];
            fixed (byte* p = snapshot) { if (api.Base.Copy(handle, 5, p, n, &n) != 0) continue; }
            var result = BridgeWire.Deserialize<ContractEditorPublication>(snapshot)!;
            var outcome = result.Outcomes.SingleOrDefault(x => x.RequestId == intent.RequestId);
            if (!result.Busy && outcome is not null && outcome.State != "Pending") { Assert.True(outcome.State == "Completed", outcome.Message); return result; }
            Thread.Sleep(100);
        } while (DateTime.UtcNow < deadline);
        throw new TimeoutException("Calendar operation did not receive backend completion");
    }
}
