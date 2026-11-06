using System.Globalization;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;

public static partial class ApiServerModes
{
    const string IronCondorCurrencyReconciliationOption = "--reconcile-iron-condor-currency-evidence";

    private static bool IsIronCondorCurrencyReconciliationArgument(string argument) =>
        string.Equals(argument, IronCondorCurrencyReconciliationOption, StringComparison.OrdinalIgnoreCase) ||
        argument.StartsWith(IronCondorCurrencyReconciliationOption + "=", StringComparison.OrdinalIgnoreCase);

    private static async Task RunIronCondorCurrencyReconciliationAsync(WebApplication app, string[] args)
    {
        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("Iron Condor currency evidence reconciliation requires Development.");
        var arguments = args.Where(IsIronCondorCurrencyReconciliationArgument).ToArray();
        var values = arguments.Length == 1 && arguments[0].Length > IronCondorCurrencyReconciliationOption.Length
            ? arguments[0][(IronCondorCurrencyReconciliationOption.Length + 1)..].Split(',') : [];
        if (values.Length != 3 || !long.TryParse(values[1], NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            || version <= 0 || !Guid.TryParse(values[2], out var commandId) || commandId == Guid.Empty)
            throw new ArgumentException($"Use {IronCondorCurrencyReconciliationOption}=<position-id>,<expected-stream-version>,<command-id>.");

        var separator = values[0].LastIndexOf('.');
        if (separator <= 0 || !Guid.TryParse(values[0][(separator + 1)..], out var positionId) || positionId == Guid.Empty)
            throw new ArgumentException("Position identity must include the Portfolio, Fund, Order, Trade and Position IDs.");
        var id = new StrategyPositionId(TradeEntityId.Parse(values[0][..separator]), positionId);
        var command = new ReconcileIronCondorPositionCurrencyEvidenceCommand
        {
            CommandId = commandId, EntityId = id, ExpectedStreamVersion = version,
            Subject = new(ActorType.Command, FuturesIronCondorTradePositionCommandActor.ActorName,
                ReconcileIronCondorPositionCurrencyEvidenceCommand.Verb, id.Format())
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var container = app.Services.GetRequiredService<SimpleInjector.Container>();
        var repository = container.GetInstance<IResidentEventSourceActorStateRepository<IronCondorPositionCommandState>>()
            as IronCondorPositionStateRepository ?? throw new InvalidOperationException("The Iron Condor source repository is unavailable.");
        var result = await repository.ReconcileLegacyCurrencyEvidenceAsync(command, deadline.Token);
        if (result == IronCondorCurrencyEvidenceReconciliation.Unavailable)
            throw new InvalidOperationException($"No qualified committed currency evidence was found for position {id.Format()}.");
        Console.WriteLine($"Iron Condor currency evidence {result} for position {id.Format()}.");
    }
}
