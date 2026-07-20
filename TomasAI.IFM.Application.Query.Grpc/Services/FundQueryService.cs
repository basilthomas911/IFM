using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Query;
using TomasAI.IFM.Application.Storage.FundDb;

namespace TomasAI.IFM.Application.Query.Grpc.Services
{
    public class FundQueryService : FundQuery.FundQueryBase
    {
        private readonly IFundDbContext _dbFund;
        private readonly ILogger<FundQueryService> _logger;

        public FundQueryService(IFundDbContext dbFund, ILogger<FundQueryService> logger)
        {
            _dbFund = dbFund;
            _logger = logger;
        }

        public override async Task<GetFundsReply> GetFunds(GetFundsRequest request, ServerCallContext context)
        {
            try
            {
                var funds = await _dbFund.DbReader.GetFundsAsync();
                var fundsReply = new GetFundsReply();
                foreach(var e in funds )
                {
                    var fund = new FundModel
                    {
                        FundId = e.FundId,
                        Name = e.Name,
                        Description = e.Description,
                        Balance = Convert.ToDouble(e.Balance),
                        CreatedOn = Timestamp.FromDateTime(e.CreatedOn.ToUniversalTime()),
                        CreatedBy = e.CreatedBy
                    };
                    foreach(var o in e.Orders)
                    {
                        var order = new FundOrderModel
                        {
                            FundId = o.FundId,
                            OrderId = o.OrderId,
                            OrderDate = Timestamp.FromDateTime(o.OrderDate.ToUniversalTime()),
                            OrderStatus = o.OrderStatus.ToString(),
                            Reference = o.Reference,
                            CreatedOn = Timestamp.FromDateTime(o.CreatedOn.ToUniversalTime()),
                            CreatedBy = o.CreatedBy,
                            UpdatedOn = Timestamp.FromDateTime(o.UpdatedOn.ToUniversalTime()),
                            UpdatedBy = o.UpdatedBy,
                        };
                        foreach(var t in o.Trades)
                        {
                            var trade = new FundOrderTradeModel
                            {
                                OrderId = t.OrderId,
                                TradeId = t.TradeId,
                                TradeType = t.TradeType.ToString(),
                                TradeDate = Timestamp.FromDateTime(t.TradeDate.ToUniversalTime()),
                                MaturityDate = Timestamp.FromDateTime(t.MaturityDate.ToUniversalTime()),
                                TradeState = t.TradeState.ToString(),
                                TradeAction = t.TradeAction.ToString(),
                                Reference = t.Reference,
                                PrimaryTrade = t.PrimaryTrade,
                                CreatedOn = Timestamp.FromDateTime(t.CreatedOn.ToUniversalTime()),
                                CreatedBy = t.CreatedBy
                            };
                            order.Trades.Add(trade);
                        }
                        fund.Orders.Add(order);
                    }
                    fundsReply.Funds.Add(fund);
                }
                return fundsReply;
            }
            catch(Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Aborted, ex.Message));
            }
        }
        
    }
}
