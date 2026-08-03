using System;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Shared.Exceptions;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests.ScyllaDb;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class ScyllaDbBulkWriteOptionsCollection
{
    public const string CollectionName = "ScyllaDB bulk-write environment options";
}

[Collection(ScyllaDbBulkWriteOptionsCollection.CollectionName)]
public sealed class ScyllaDbBulkWriteOptionsTests
{
    [Fact]
    public void FromEnvironment_UsesValidatedConfiguredValues()
        => WithEnvironment("12", "24", () =>
        {
            var options = ScyllaDbBulkWriteOptions.FromEnvironment();

            options.MaxConcurrency.Should().Be(12);
            options.BoundedCapacity.Should().Be(24);
        });

    [Fact]
    public void FromEnvironment_RejectsCapacityBelowConcurrency()
        => WithEnvironment("32", "16", () =>
            FluentActions.Invoking(ScyllaDbBulkWriteOptions.FromEnvironment)
                .Should().Throw<StorageException>()
                .WithMessage("*must be greater than or equal*") );

    [Fact]
    public void FromEnvironment_RejectsInvalidConcurrency()
        => WithEnvironment("0", "64", () =>
            FluentActions.Invoking(ScyllaDbBulkWriteOptions.FromEnvironment)
                .Should().Throw<StorageException>()
                .WithMessage("*between 1 and 1024*") );

    static void WithEnvironment(string concurrency, string capacity, Action assertion)
    {
        var originalConcurrency = Environment.GetEnvironmentVariable(ScyllaDbBulkWriteOptions.MaxConcurrencyVariable);
        var originalCapacity = Environment.GetEnvironmentVariable(ScyllaDbBulkWriteOptions.BoundedCapacityVariable);
        try
        {
            Environment.SetEnvironmentVariable(ScyllaDbBulkWriteOptions.MaxConcurrencyVariable, concurrency);
            Environment.SetEnvironmentVariable(ScyllaDbBulkWriteOptions.BoundedCapacityVariable, capacity);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ScyllaDbBulkWriteOptions.MaxConcurrencyVariable, originalConcurrency);
            Environment.SetEnvironmentVariable(ScyllaDbBulkWriteOptions.BoundedCapacityVariable, originalCapacity);
        }
    }
}
