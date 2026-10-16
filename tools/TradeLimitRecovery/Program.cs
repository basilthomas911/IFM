using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--apply"))
    throw new ArgumentException("Usage: TradeLimitRecovery <portfolio.fund.order.trade> [--apply]. Reads the application configuration automatically.");
var parts = args[0].Split('.').Select(int.Parse).ToArray();
if (parts.Length != 4) throw new ArgumentException("A full portfolio.fund.order.trade identity is required.");
var id = new TradeEntityId(parts[0],parts[1],parts[2],parts[3]);
if (!id.IsValid) throw new ArgumentException("Invalid trade identity.");
var configuration = RecoveryConfiguration.Load();
await using var connection = new NpgsqlConnection(configuration.Postgres);
await connection.OpenAsync();
await using var read = new NpgsqlCommand("SELECT t.payload,a.definition::text,EXISTS(SELECT 1 FROM trade.trade_limit l WHERE l.tradeid=t.tradeid) FROM trade.established_trade t JOIN portfolio.accepted_trade_order a ON a.portfolio_id=t.portfolioid AND a.fund_id=t.fundid AND a.order_id=t.orderid WHERE t.portfolioid=$1 AND t.fundid=$2 AND t.orderid=$3 AND t.tradeid=$4;",connection);
foreach (var part in parts) read.Parameters.Add(new NpgsqlParameter { Value = part });
await using var reader = await read.ExecuteReaderAsync();
if (!await reader.ReadAsync()) throw new InvalidOperationException("The established trade and its original Portfolio approval were not found.");
var trade = MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(reader.GetFieldValue<byte[]>(0));
var order = (System.Text.Json.JsonSerializer.Deserialize<PortfolioExecutionOrderInstruction>(reader.GetString(1))
    ?? throw new InvalidOperationException("Invalid original approval.")).ToTradeOrder();
var exists = reader.GetBoolean(2);
await reader.CloseAsync();
if (exists) { Console.WriteLine($"{id.Format()}: limits already exist; preserved."); return; }
var snapshot = ApprovedTradeMonitoringLimits.Create(order,trade);
Console.WriteLine($"{id.Format()}: original approval {snapshot.PortfolioApprovalId}; capital={snapshot.TradeLimit.RiskMargin}; loss={snapshot.TradeLimit.MaxLoss}; profit basis={snapshot.TradeLimit.MaxProfit}; spreads={snapshot.SpreadLimits.Length}.");
if (args.Length == 1) { Console.WriteLine("Preview only. Add --apply after the API has been rebuilt and restarted."); return; }
await using var producer = new NatsActorProducer(new NatsProducerOptions { Url = configuration.NatsUrl },NullLogger.Instance);
await producer.StartAsync(new(ActorType.Command,"ApprovedTradeLimitRecovery"));
var command = ApprovedTradeLimitRecovery.CreateCommand(order,trade);
var result = await producer.RequestAsync<InitializeIronCondorMonitoringCommand,StrategyPositionId,TomasAI.IFM.Shared.EventSourcing.GuidResult>(command.Subject,command,command.EntityId);
if (!result.Success) throw new InvalidOperationException($"Recovery failed ({result.ErrorCode}): {result.ErrorMessage}");
await using var verify = new NpgsqlCommand("SELECT count(*) FROM trade.trade_limit WHERE tradeid=$1 AND riskmargin=$2 AND maxloss=$3;",connection);
verify.Parameters.Add(new NpgsqlParameter { Value = id.TradeId });
verify.Parameters.Add(new NpgsqlParameter { Value = snapshot.TradeLimit.RiskMargin });
verify.Parameters.Add(new NpgsqlParameter { Value = snapshot.TradeLimit.MaxLoss });
if (Convert.ToInt64(await verify.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("Recovery was acknowledged but the expected limits are missing.");
Console.WriteLine("Recovery committed and verified. No orders were submitted or replayed.");
