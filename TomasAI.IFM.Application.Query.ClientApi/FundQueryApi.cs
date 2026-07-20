using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using TomasAI.IFM.Application.Query.Grpc;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Query.Client.Api
{
    public class FundQueryApi : IFundQueryApi
    {
        private readonly string _baseUri;
        private readonly FundQuery.FundQueryClient _client;

        public FundQueryApi(string baseUri)
        {
            var channel = GrpcChannel.ForAddress(baseUri);
            _client = new FundQuery.FundQueryClient(channel);
        }

        public Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(int fundId, DateTime valueDate)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync(int fundId)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync(int orderId)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(int fundId, DateTime startDate, DateTime endDate)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult<FundReadModel[]>> GetFundsAsync()
        {
            var serviceResult = default(ServiceResult<FundReadModel[]>);
            try
            {
                var reply = await _client.GetFundsAsync(new GetFundsRequest { });
                var funds = new List<FundReadModel>();
                foreach(var e in reply.Funds)
                {
                    var fund = new FundReadModel(
                        fundId: e.FundId,
                        name: e.Name,
                        description: e.Description,
                        balance: Convert.ToDecimal(e.Balance),
                        createdOn: e.CreatedOn.ToDateTime(),
                        createdBy: e.CreatedBy);
                    foreach(var o in e.Orders)
                    {
                        var order = new FundOrderReadModel(
                            fundId: e.FundId,
                            orderId: o.OrderId,
                            orderDate: o.OrderDate.ToDateTime(),
                            orderStatus: (Shared.Fund.OrderStatus)(System.Enum.Parse(typeof(Shared.Fund.OrderStatus), o.OrderStatus)),
                            reference: o.Reference,
                            createdOn: o.CreatedOn.ToDateTime(),
                            createdBy: o.CreatedBy,
                            updatedOn: o.UpdatedOn.ToDateTime(),
                            updatedBy: o.UpdatedBy
                        );
                        foreach (var t in o.Trades)
                        {
                            var trade = new FundOrderTradeReadModel(
                                orderId: t.OrderId,
                                tradeId: t.TradeId,
                                tradeType: (TradeType)(System.Enum.Parse(typeof(TradeType), t.TradeType)),
                                tradeDate: t.TradeDate.ToDateTime(),
                                maturityDate: t.MaturityDate.ToDateTime(),
                                tradeState: (TradeState)(System.Enum.Parse(typeof(TradeState), t.TradeState)),
                                tradeAction: (TradeAction)(System.Enum.Parse(typeof(TradeAction), t.TradeAction)),
                                reference: t.Reference,
                                primaryTrade: t.PrimaryTrade,
                                createdOn: t.CreatedOn.ToDateTime(),
                                createdBy: t.CreatedBy
                            );
                            order.Add(trade);
                        }
                        fund.Add(order);
                    }
                    funds.Add(fund);
                }
                serviceResult = new ServiceOk<FundReadModel[]>(funds.ToArray());
            }
            catch (RpcException ex)
            {
                serviceResult = new ServiceFailed<FundReadModel[]>(1234, ex.Status.Detail);
            }
            catch(Exception ex)
            {
                serviceResult = new ServiceFailed<FundReadModel[]>(1234, ex.Message);
            }
            return serviceResult;
        }

        public Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(int fundId, DateTime startDate, DateTime endDate)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(int fundId, DateTime valueDate)
        {
            throw new NotImplementedException();
        }
    }
}
