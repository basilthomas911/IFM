using TomasAI.IFM.Api.DatabaseBackup.Host.Services.Recovery;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class ScyllaNativeClusterIdentityTests
{
    [Fact]
    public void Native_cluster_name_can_be_read_when_cql_compatibility_name_is_empty()
        => Assert.Equal("Test Cluster", DevelopmentDatabaseReplacementPlatform.ReadScyllaClusterName(
            "Cluster Information:\n\tName: Test Cluster\n\tSnitch: org.apache.cassandra.locator.SimpleSnitch\n"));

    [Theory]
    [InlineData("{\"cluster_name\": \"\"}")]
    [InlineData("Cluster Information:\n\tName: \n\tSnitch: SimpleSnitch\n")]
    [InlineData("Cluster Information:\n\tSnitch: SimpleSnitch\n")]
    public void Missing_native_name_cannot_qualify_cluster_compatibility(string description)
        => Assert.Throws<InvalidDataException>(() => DevelopmentDatabaseReplacementPlatform.ReadScyllaClusterName(description));
}