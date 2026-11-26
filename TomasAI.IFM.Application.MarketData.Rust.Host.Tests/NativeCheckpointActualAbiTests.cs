using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Tests;

public sealed class NativeCheckpointActualAbiTests
{
    sealed class NativeFixtureFactAttribute : FactAttribute
    {
        public NativeFixtureFactAttribute()
        {if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_TEST_NATIVE_CHECKPOINT_LIBRARY"))||string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_TEST_NATIVE_CHECKPOINT_FIXTURE")))Skip="Requires the actual native DLL and explicitly synthetic typed owner fixture.";}
    }
    [NativeFixtureFact]
    public void Real_native_ABI_validates_and_stages_the_original_typed_graph_and_rejects_inner_checksum_corruption()
    {
        var library=Environment.GetEnvironmentVariable("IFM_TEST_NATIVE_CHECKPOINT_LIBRARY")!;
        var fixture=Environment.GetEnvironmentVariable("IFM_TEST_NATIVE_CHECKPOINT_FIXTURE")!;
        var metadata=JsonDocument.Parse(File.ReadAllBytes(Path.ChangeExtension(fixture,"fixture.json")));
        Assert.Equal("SyntheticPrivateOwnerFixture",metadata.RootElement.GetProperty("Kind").GetString());
        Assert.False(metadata.RootElement.GetProperty("Qualified").GetBoolean());Assert.False(metadata.RootElement.GetProperty("ProviderStarted").GetBoolean());
        var scope=metadata.RootElement.GetProperty("Scope").GetString()!;var expected=metadata.RootElement.GetProperty("CheckpointSha256").GetString()!;
        var prior=Environment.GetEnvironmentVariable("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED");var root=Path.Combine(Path.GetTempPath(),"ifm-native-staging-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            Environment.SetEnvironmentVariable("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED","1");
            var first=NativeMarketDataRuntime.StageNativeCheckpoint(library,scope,fixture,expected);
            Assert.Equal(expected,first.Sha256);Assert.Equal(first,NativeMarketDataRuntime.StageNativeCheckpoint(library,scope,fixture,expected));
            var source=metadata.RootElement.GetProperty("OriginalSourceRoot").GetString()!;
            foreach(var item in metadata.RootElement.GetProperty("Files").EnumerateArray())
            {
                var relative=item.GetProperty("RelativePath").GetString()!;Assert.False(Path.IsPathRooted(relative));Assert.DoesNotContain("..",relative.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
                var target=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(Path.Combine(source,relative),target);
            }
            NativeMarketDataRuntime.ConfigureNativeCheckpointRestoreRoot(library,scope,root);
            Assert.Throws<InvalidDataException>(()=>NativeMarketDataRuntime.StageNativeCheckpoint(library,scope,fixture,new string('0',64)));
            var corrupted=File.ReadAllBytes(fixture);corrupted[^1]^=1;var invalid=Path.Combine(root,"corrupted.checkpoint");File.WriteAllBytes(invalid,corrupted);
            // Supply the correct outer file hash: rejection must come from the actual Rust embedded codec/checksum.
            var outer=Convert.ToHexString(SHA256.HashData(corrupted)).ToLowerInvariant();
            Assert.Throws<InvalidOperationException>(()=>NativeMarketDataRuntime.StageNativeCheckpoint(library,scope,invalid,outer));
        }
        finally{Environment.SetEnvironmentVariable("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED",prior);Directory.Delete(root,true);}
    }
}
