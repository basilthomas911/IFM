using System.Data;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.Postgres;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Postgres;

public class PostgresProviderCompatibilityTests
{
    [Fact]
    public void GetCommandDefinition_CustomContext_UsesSetCommandContract()
    {
        var repository = Substitute.For<IObjectRepository>();
        repository.Schema.Returns("custom_schema");
        var context = Substitute.For<IObjectRepositoryContext>();
        context.Repository.Returns(repository);
        context.When(candidate => candidate.SetCommand(Arg.Any<IDbCommand>()))
            .Do(callInfo =>
            {
                var command = callInfo.Arg<IDbCommand>();
                command.CommandText = "save_event";
                command.CommandType = CommandType.StoredProcedure;
            });

        var definition = PostgresObjectDataRepositoryProvider.GetCommandDefinition(context);

        definition.CommandText.Should().Be("custom_schema.save_event");
        definition.CommandType.Should().Be(CommandType.StoredProcedure);
        context.Received(1).SetCommand(Arg.Any<IDbCommand>());
    }
}
