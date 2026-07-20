using System;
using System.Collections.Generic;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.UnitTests.Postgres.EventSourceDb;

public static class SampleData
{
    public static CommandLogReadModel CommandLog => new (
        CommandId: Guid.NewGuid(),
        StreamId: "sample-aggregate-id",
        AggregateName: BoundedContextName.FundBoundedContext,
        CommandName: "CreateFundCommand",
        CommandTimestamp: DateTime.Now,
        CommandData: "{\"FundId\": 1, \"Name\": \"Sample Fund\", \"Description\": \"This is a sample fund.\", \"Balance\": 1000000.00, \"IsProduction\": true, \"CreatedOn\": \"2023-10-01T00:00:00Z\", \"CreatedBy\": \"Admin\"}"
    );

    public static List<CommandLogReadModel> CommandLogs =>
    [
        new (
            CommandId: Guid.NewGuid(),
            StreamId: "sample-aggregate-id-1",
            AggregateName: BoundedContextName.FundBoundedContext,
            CommandName: "CreateFundCommand",
            CommandTimestamp: DateTime.UtcNow,
            CommandData: "{\"FundId\": 1, \"Name\": \"Sample Fund 1\", \"Description\": \"This is a sample fund 1.\", \"Balance\": 1000000.00, \"IsProduction\": true, \"CreatedOn\": \"2023-10-01T00:00:00Z\", \"CreatedBy\": \"Admin\"}"
        ),
        new (
            CommandId: Guid.NewGuid(),
            StreamId: "sample-aggregate-id-2",
            AggregateName: BoundedContextName.FundBoundedContext,
            CommandName: "UpdateFundCommand",
            CommandTimestamp: DateTime.Now,
            CommandData: "{\"FundId\": 2, \"Name\": \"Sample Fund 2\", \"Description\": \"This is a sample fund 2.\", \"Balance\": 2000000.00, \"IsProduction\": false, \"CreatedOn\": \"2023-10-02T00:00:00Z\", \"CreatedBy\": \"Admin\"}"
        )
    ];
}


