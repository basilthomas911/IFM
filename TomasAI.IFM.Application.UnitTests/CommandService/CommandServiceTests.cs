using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using System.Threading.Tasks.Dataflow;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TomasAI.IFM.Application.Storage.EventDb;
using TomasAI.IFM.Application.Services;
using TomasAI.IFM.Application.Trade;
using TomasAI.IFM.Application.Fund;
using TomasAI.IFM.Application.Fund.Repository;
using TomasAI.IFM.Application.Fund.Commands;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.Commands;
using Moq;

namespace TomasAI.IFM.UnitTests.CommandService
{
    [TestClass]
    public class CommandServiceTests
    {
        [TestMethod]
        public void CommandServiceOk()
        {
            var connSet = new DbConnectionSettings()
                .Add("EventDbConnection", "Data Source=DEV-SERVER;Initial Catalog=eventdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var dbEventLog = new EventDbContext(connSet);
            var repo = new FundRepository(dbEventLog, null);
            var chr = new CommandHandlerResolver(e =>
                e == typeof(ICommandHandler<CreateFundCommand>)
                        ? new FundCommands(repo)
                        : null);
            var cab = new CommandActionBlock(chr);
            var cmdSvc = new TomasAI.IFM.Application.Services.CommandService(cab);
            //var fundSvc = new FundService(cmdSvc, null);
            //var svcResult = fundSvc.CreateFund(1001, "BlackDiamond-USDEquityLargeCap-2018", "");
            //Assert.IsTrue(svcResult.Success);
            //svcResult = fundSvc.CreateFund(1001, "BlackDiamond-USDEquityLargeCap-2018", "");
            //Assert.IsFalse(svcResult.Success);
        }
    }
}
