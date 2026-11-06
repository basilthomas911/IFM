using System.Globalization;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;

public static partial class ApiServerModes
{
    static async Task ReconcileExitWorkflowAsync(WebApplication app, string[] args)
    {
        var key = args.SingleOrDefault(static arg => arg.StartsWith("--reconcile-exit-workflow=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1];
        var strategy = args.SingleOrDefault(static arg => arg.StartsWith("--exit-workflow-strategy=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1];
        var kind = strategy?.ToUpperInvariant() switch
        {
            "FUTURESOUTRIGHT" => TradeStrategyKind.FuturesOutright,
            "VERTICALSPREAD" => TradeStrategyKind.VerticalSpread,
            "IRONCONDOR" => TradeStrategyKind.IronCondor,
            _ => throw new ArgumentException("Set --exit-workflow-strategy to FuturesOutright, VerticalSpread or IronCondor.")
        };
        var workflow = ParseExitWorkflowId(key);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var transactions = app.Services.GetRequiredService<IPostgresEventTransaction>();
        var producer = app.Services.GetRequiredService<IActorProducer>();
        // Use the running command owner so the same receipt and concurrency fences apply.
        await producer.StartAsync(new(ActorType.Command, "ExitWorkflowReconciliation"), deadline.Token);
        try
        {
            var advances = 0;
            while (advances < 3 && await ExitWorkflowLegacyReconciliation.ReconcileAsync(workflow, kind, transactions, producer, deadline.Token))
                advances++;
            Console.WriteLine($"Exit workflow {workflow.Format()} reconciliation accepted {advances} transition(s) from committed evidence.");
        }
        finally
        {
            await producer.StopAsync(CancellationToken.None);
        }
    }

    static ExitPositionWorkflowId ParseExitWorkflowId(string? key)
    {
        var parts = key?.Split('.', StringSplitOptions.None);
        if (parts is not { Length: 7 } ||
            !Guid.TryParseExact(parts[4], "N", out var position) ||
            !DateOnly.TryParseExact(parts[5], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
            !Guid.TryParseExact(parts[6], "N", out var decision))
            throw new FormatException("Set --reconcile-exit-workflow to PortfolioId.FundId.OrderId.TradeId.PositionId.yyyyMMdd.ExitDecisionId with GUIDs in N format.");
        var trade = TradeEntityId.Parse(string.Join(".", parts, 0, 4));
        var id = new ExitPositionWorkflowId(new(trade, position), date, decision);
        return id.IsValid ? id : throw new FormatException("Exit workflow position and decision GUIDs must be nonempty.");
    }
}
