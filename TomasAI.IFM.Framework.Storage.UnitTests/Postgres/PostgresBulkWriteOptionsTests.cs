using System;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Exceptions;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Postgres;

[Collection(PostgresBulkWriteOptionsCollection.Name)]
public sealed class PostgresBulkWriteOptionsTests
{
    [Fact]
    public void FromEnvironment_UsesDefaultWhenVariableIsMissing()
        => WithEnvironment(null, () =>
            PostgresBulkWriteOptions.FromEnvironment().BatchSize.Should().Be(256));

    [Fact]
    public void FromEnvironment_UsesConfiguredBatchSize()
        => WithEnvironment("512", () =>
            PostgresBulkWriteOptions.FromEnvironment().BatchSize.Should().Be(512));

    [Theory]
    [InlineData("0")]
    [InlineData("4097")]
    [InlineData("invalid")]
    public void FromEnvironment_RejectsInvalidBatchSize(string value)
        => WithEnvironment(value, () =>
            FluentActions.Invoking(PostgresBulkWriteOptions.FromEnvironment)
                .Should().Throw<StorageException>()
                .WithMessage("*between 1 and 4096*"));

    static void WithEnvironment(string? value, Action assertion)
    {
        var original = Environment.GetEnvironmentVariable(PostgresBulkWriteOptions.BatchSizeVariable);
        try
        {
            Environment.SetEnvironmentVariable(PostgresBulkWriteOptions.BatchSizeVariable, value);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(PostgresBulkWriteOptions.BatchSizeVariable, original);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresBulkWriteOptionsCollection
{
    public const string Name = "PostgreSQL bulk-write environment options";
}
