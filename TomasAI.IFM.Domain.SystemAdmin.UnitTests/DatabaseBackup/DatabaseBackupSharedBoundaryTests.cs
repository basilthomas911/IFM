using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Translation;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

public sealed class DatabaseBackupSharedBoundaryTests
{
    [Fact]
    public void Contracts_do_not_reference_the_domain_implementation()
    {
        var contracts = typeof(RequestDatabaseBackupCommand).Assembly;
        var domain = typeof(DatabaseBackupCommandActor).Assembly;

        Assert.NotEqual(contracts, domain);
        Assert.DoesNotContain(contracts.GetReferencedAssemblies(), reference => reference.Name == domain.GetName().Name);
        Assert.Contains(domain.GetReferencedAssemblies(), reference => reference.Name == contracts.GetName().Name);
    }

    [Theory]
    [InlineData("Model.DatabaseBackupChange")]
    [InlineData("Model.DatabaseBackupTransition")]
    [InlineData("Model.DatabaseBackupEventFactory")]
    [InlineData("Model.DatabaseOperationRules")]
    [InlineData("Model.DatabaseOperationChanges")]
    [InlineData("Model.DatabaseOperationReceiptComputation")]
    [InlineData("Translation.DatabaseBackupEventTranslator")]
    [InlineData("EventHandling.DatabaseBackupServiceEventHandler")]
    [InlineData("Query.DatabaseBackupQueryReply")]
    public void Shared_helpers_belong_to_the_domain_assembly(string name)
    {
        var fullName = $"TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.{name}";
        var domain = typeof(DatabaseBackupEventTranslator).Assembly;

        Assert.NotNull(domain.GetType(fullName));
        Assert.Null(typeof(RequestDatabaseBackupCommand).Assembly.GetType(fullName));
    }
}
