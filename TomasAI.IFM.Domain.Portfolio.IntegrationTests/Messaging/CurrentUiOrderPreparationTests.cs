using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

/// <summary>Real transport, running API actor, PostgreSQL reference repositories and current UI consumer.</summary>
public class CurrentUiOrderPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_composer_uses_server_economics_or_reports_missing_reference_without_submitting(bool missingTick)
    {
        var url = Environment.GetEnvironmentVariable("IFM_TEST_NATS_URL")
            ?? throw new InvalidOperationException("An isolated broker is required.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, NullLogger.Instance);
        await producer.StartAsync(new(ActorType.Query, "OrderPreparationClient" + Guid.NewGuid().ToString("N")), deadline.Token);
        try
        {
            var host = KestrelWebApplicationFactory<ApiServerEntryPoint>.StartShared();
            var db = host.GetRequiredService<IDbContextFactory>();
            var config = host.GetRequiredService<IConfiguration>();
            var alias = config["TradeBroker:Emulator:AccountAlias"] ?? config["TradeBroker:AccountAlias"] ?? "IFM-EMULATOR-PAPER";
            var fundId = Random.Shared.Next(100000, 100000000);
            var contractId = "PREP" + Guid.NewGuid().ToString("N");
            var reference = new FuturesContractV3ReadModel(contractId,"ESZ6","ES","ESZ6","FUT","USD","XCME","50",new(2026,12,18),true,true)
                { TickSize = missingTick ? null : .25m };
            await db.SecuritiesDb.InsertFuturesContractAsync(reference);
            var fund = new FundMandateReadModel { PortfolioId=1, FundId=fundId, FundMandateVersion=1,
                FundCode="PREP"+fundId, Name="Preparation test", TradingYear=2026, CreatedBy="integration", CreatedOnUtc=DateTime.UtcNow };
            await db.PortfolioDb.UpsertFundAsync(PortfolioProjection<FundMandateReadModel>.Create(fund,1,fundId,DateTime.UtcNow));
            var services = Substitute.For<IUiServiceCatalog>();
            services.TradeQueries.Returns(new TradeQueryService(new OptionTradeQueryApi(producer)));
            var root = Substitute.For<IAppRoot>(); root.Services.Returns(services);
            root.TradeExecution.Returns(new DesktopTradeExecutionContext(alias,BrokerEnvironment.Emulator));
            root.DiagnosticLogger.Returns(NullLogger.Instance);
            var model = new BrokerManualTradeOrderViewModel(root, 1,
                new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { PortfolioId=1,FundId=fundId,OrderId=3 }),
                new() { FundId=fundId,OrderId=3,TradeId=4,TradeType=TradeType.FuturesOutright,TradeAction=TradeAction.Buy,
                    RequestedTradeDate=new(2026,10,15),PrimaryTrade=true,InstructionReference=contractId },
                // Deliberately stale client multiplier: confirmation must use the backend definition of 50.
                reference with { Multiplier="999" });
            var dialog = new CancelDialog();
            var pending = model.SubmitAsync(1, 10m, dialog, deadline.Token);
            if (missingTick)
            {
                await FluentActions.Awaiting(async () => await pending).Should().ThrowAsync<InvalidOperationException>().WithMessage("*tick metadata*");
                dialog.Order.Should().BeNull();
            }
            else
            {
                (await pending).Should().Be(Guid.Empty, "operator cancellation must not place an order");
                dialog.Order!.OrderAmount.Should().Be(500m); dialog.Order.Commission.Should().Be(.65m);
                dialog.Order.TotalAmount.Should().Be(500.65m);
                dialog.Order.ValueDate.Should().Be(host.GetRequiredService<IFuturesMarketSessionAuthority>().Current.OperationalValueDate);
            }
            services.ReceivedCalls().Should().NotContain(c => c.GetMethodInfo().Name == "get_PortfolioTradeOrders");
        }
        finally { await producer.StopAsync(deadline.Token); }
    }

    sealed class CancelDialog : ITradeOrderConfirmationService
    {
        public TradeOrderReadModel? Order;
        public ValueTask<TradeOrderConfirmationResult> ConfirmAsync(TradeOrderReadModel order, CancellationToken token = default)
        { Order = order; return ValueTask.FromResult(new TradeOrderConfirmationResult(false, TradeFillType.Broker)); }
    }
}
