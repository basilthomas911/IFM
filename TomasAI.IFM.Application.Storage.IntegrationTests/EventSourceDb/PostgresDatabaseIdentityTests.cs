using TomasAI.IFM.Application.Storage.EventSourceDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class PostgresDatabaseIdentityTests
{
    [Fact]
    public void Host_case_does_not_change_database_identity()
    {
        Assert.Equal(
            PostgresDatabaseIdentity.Parse("Host=LOCALHOST;Database=trading;Username=owner"),
            PostgresDatabaseIdentity.Parse("Host=localhost;Database=trading;Username=owner"));
    }

    [Theory]
    [InlineData("Database=Trading;Username=owner")]
    [InlineData("Database=trading;Username=Owner")]
    public void Database_and_user_case_remain_part_of_identity(string destination)
    {
        Assert.NotEqual(
            PostgresDatabaseIdentity.Parse("Host=localhost;Database=trading;Username=owner"),
            PostgresDatabaseIdentity.Parse("Host=localhost;" + destination));
    }
}
