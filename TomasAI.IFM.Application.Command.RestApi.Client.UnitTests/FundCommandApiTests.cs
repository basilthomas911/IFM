using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Commands;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Application.Command.Client;

namespace TomasAI.IFM.Application.Command.RestApi.Client.UnitTests
{
    public class FundCommandApiTests
    {
        [Fact]
        public async Task CreateFundAsyncOk()
        {
            // given a command service...
            var command = default(CreateFundCommand);
            var commandService = Substitute.For<ICommandService>();
            commandService
                .PostApiCommandAsync(Arg.Any<CreateFundCommand>(), Arg.Any<string>())
                .Returns(e => {
                    command = e.ArgAt<CreateFundCommand>(0);
                    return new ServiceOk<Guid>(command.CommandId); 
                });

            // when executing fundCommandApi.CreateFundAsync...
            var fundCommandApi = new FundCommandApi(commandService);
            var serviceResult = await fundCommandApi.CreateFundAsync(new FundReadModel(
                fundId: 1234, 
                name: "TestFund",
                description: "TestFund description",
                balance: 1000.00m,
                isProduction: false,
                createdOn: new DateTime(2020,10,10),
                createdBy: "basil"));

            // then command posted successfully and valid command was created.
            serviceResult.Should().NotBeNull();
            serviceResult.Success.Should().BeTrue();

            command.Should().NotBeNull();
            command.CommandId.Should().NotBeEmpty();
            command.NewFund.Should().NotBeNull();
            command.NewFund.FundId.Should().Be(1234);
            command.NewFund.Name.Should().NotBeNull();
            command.NewFund.Name.Should().Be("TestFund");
            command.NewFund.Description.Should().NotBeNull();
            command.NewFund.Description.Should().Be("TestFund description");
            command.NewFund.Balance.Should().Be(1000.00m);
            command.NewFund.CreatedOn.Should().Be(new DateTime(2020, 10, 10));
            command.NewFund.CreatedBy.Should().NotBeNull();
            command.NewFund.CreatedBy.Should().Be("basil");
        }
    }
}
