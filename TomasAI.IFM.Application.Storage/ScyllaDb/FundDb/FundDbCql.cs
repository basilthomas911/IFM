namespace TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;

internal class FundDbCql
{
    public static string GetFundTransaction = """
        SELECT 
            transactionId AS "TransactionId",   
            transactionDate AS "TransactionDate",
            transactionType AS "TransactionType",
            fundId AS "FundId",
            orderId AS "OrderId",
            tradeId AS "TradeId",
            tradeType AS "TradeType",
            valueDate AS "ValueDate",
            tradeStatus AS "TradeStatus",
            description AS "Description",
            amount AS "Amount",
            balance AS "Balance"
        FROM fund_transaction
        WHERE fundId = :fundId
          AND valueDate = :valueDate
          AND orderId = :orderId
          AND tradeId = :tradeId
          AND tradeType = :tradeType
          AND transactionType = :transactionType
          AND transactionDate = :transactionDate;
        """;

    public const string DeleteFundTransaction = """
    DELETE FROM fund_transaction
    WHERE fundId = :fundId
      AND valueDate = :valueDate
      AND orderId = :orderId
      AND tradeId = :tradeId
      AND tradeType = :tradeType
      AND transactionType = :transactionType
      AND transactionDate = :transactionDate;
    """;

    public const string GetFundOrderTrade = """
        SELECT 
            fundId AS "FundId", 
            orderId AS "OrderId", 
            tradeId AS "TradeId", 
            tradeType AS "TradeType", 
            tradeDate AS "TradeDate", 
            maturityDate AS "MaturityDate", 
            tradeState AS "TradeState", 
            tradeAction AS "TradeAction", 
            reference AS "Reference", 
            primaryTrade AS "PrimaryTrade", 
            baseContractSymbol AS "BaseContractSymbol", 
            createdOn AS "CreatedOn", 
            createdBy AS "CreatedBy", 
            updatedOn AS "UpdatedOn", 
            updatedBy AS "UpdatedBy" 
        FROM fund_order_trade 
        where fundId = :fundId
        and orderId = :orderId
        and tradeId = :tradeId;
        """;

     public const string GetFundTransactionByTradeStatus = """
        select min(TransactionDate) as "Value"
        from fund_transaction 
        where FundId = :fundId 
        and ValueDate >= :startDate
        group by FundId 
        allow filtering;
        """;

    // Added from FundDbCql.Designer.cs
    public const string CreateFundOrderTable = """
        CREATE TABLE IF NOT EXISTS fund_order (
           FundId int,
           OrderId int,
           OrderDate timestamp,
           OrderStatus text,
           BaseContractId text,
           TradeDate timestamp,
           MaturityDate timestamp,
           Reference text,
           CreatedOn timestamp,
           CreatedBy text,
           UpdatedOn timestamp,
           UpdatedBy text,
           PRIMARY KEY (FundId, OrderId)
        );
        """;

    public const string CreateFundOrderTradeTable = """
        CREATE TABLE IF NOT EXISTS fund_order_trade (
           fundId int,
           orderId int,
           tradeId int,
           tradeType text,
           tradeDate timestamp,
           maturityDate timestamp,
           tradeState text,
           tradeAction text,
           reference text,
           primaryTrade boolean,
           baseContractSymbol text,
           createdOn timestamp,
           createdBy text,
           updatedOn timestamp,
           updatedBy text,
           PRIMARY KEY ((fundId, orderId), tradeId)
        );
        """;

    public const string CreateFundTable = """
        CREATE TABLE IF NOT EXISTS fund (
           FundId int PRIMARY KEY,
           Name text,
           Description text,
           Balance decimal,
           IsProduction boolean,
           CreatedOn timestamp,
           CreatedBy text
        )
        """;

    public const string CreateFundTransactionTable = """
        CREATE TABLE IF NOT EXISTS fund_transaction (
           transactionId bigint,	
           transactionDate timestamp,
           transactionType text,
           fundId int,
           orderId int,
           tradeId int,
           tradeType text,
           valueDate timestamp,
           tradeStatus text,
           description text,
           amount decimal,
           balance decimal,
           PRIMARY KEY (fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate, transactionId)
        );
        """;

    public const string DeleteFund = """
        DELETE FROM fund WHERE fundId = :fundId;
        """;

    public const string DeleteFundOrder = """
        DELETE FROM fund_order WHERE fundId = :fundId AND orderId = :orderId;
        """;

    public const string DeleteFundOrderTrade = """
        DELETE FROM fund_order_trade WHERE fundId = :fundId AND orderId = :orderId AND tradeId = :tradeId;
        """;

    public const string GetFundBalance = """
        select balance as "Value"
        from fund
        where fundId = :fundId;
        """;

    public const string GetFundBalanceByTransactionDate = """
        select Balance as "Value"
        from fund_transaction
        where FundId = :fundId 
        and TransactionDate = :transactionDate
        allow filtering;
        """;

    public const string GetFundBalanceByTransactionId = """
        select Balance as "Value"
        from fund_transaction
        where FundId = :fundId 
        and TransactionId = :transactionId
        allow filtering;
        """;
    public const string GetFundByFundId = """
        SELECT fundId AS "FundId", 
              name AS "Name", 
              description AS "Description", 
              balance AS "Balance", 
              isProduction AS "IsProduction", 
              createdOn AS "CreatedOn", 
              createdBy AS "CreatedBy" 
        FROM fund 
        WHERE fundId = :fundId;
        """;

    public const string GetFundDailyBalance = """
        select FundId as "FundId",ValueDate as "ValueDate", max(Balance) as "Balance"
        from fund_transaction
        where FundId = :fundId
        and ValueDate >= :startDate
        and ValueDate <= :endDate
        group by FundId, ValueDate
        order by ValueDate desc;
        """;

    public const string GetFundIdFromOrderId = """
        select FundId as "Value"
        from fund_order
        where orderId = :orderId;
        """;

    public const string GetFundLossOrders = """
        select FundId as "FundId", 
        ValueDate as "ValueDate",
        OrderId as "OrderId", 
        sum(Amount) as "Amount"
        from fund_transaction
        where FundId = :fundId 
        and ValueDate >= :startDate 
        and ValueDate <= :endDate
        and TransactionType in ('RealizedTradePnlAdjustment','UnrealizedTradePnl','UnrealizedTradePnlAdjustment','TradeCommission','OpeningTradeAdjustment','RealizedTradePnl','TradeCommissionAdjustment')
        and Amount < 0
        group by FundId, ValueDate, OrderId 
        allow filtering;
        """;

    public const string GetFundMaxTransactionDate = """
        select max(TransactionDate) as "Value"
        from fund_transaction 
        where FundId = :fundId 
        and ValueDate <= :endDate
        group by FundId
        allow filtering;
        """;

    public const string GetFundMinTransactionDate = """
        select min(TransactionDate) as "Value"
        from fund_transaction 
        where FundId = :fundId 
        and ValueDate >= :startDate
        group by FundId
        allow filtering;
        """;

    public const string GetFundMaxTransactionDateByTradeStatus = """
        select max(TransactionDate) as "Value" 
        from fund_transaction 
        where FundId = :fundId 
        and ValueDate = :valueDate 
        and TradeStatus = :tradeStatus 
        allow filtering;
        """;

    public const string GetFundMinTransactionDateByTradeStatus = """
        select min(TransactionDate) as "Value" 
        from fund_transaction 
        where FundId = :fundId 
        and ValueDate = :valueDate 
        and TradeStatus = :tradeStatus
        allow filtering;
        """;

    public const string GetFundMaxTransactionId = """
        select max(TransactionId) as "Value"
        from fund_transaction 
        where FundId = :fundId 
        and ValueDate <= :endDate
        group by FundId
        allow filtering;
        """;

    public const string GetFundMinTransactionId = """
        select min(TransactionId) as "Value"
        from fund_transaction 
        where FundId = :fundId 
        and ValueDate >= :startDate
        group by FundId
        allow filtering;
        """;

    public const string GetFundMaxTransactionIdByTradeStatus = """
        select max(TransactionId) as "Value" 
        from fund_transaction 
        where FundId = :fundId 
        and ValueDate = :valueDate 
        and TradeStatus = :tradeStatus 
        allow filtering;
        """;

    public const string GetFundMinTransactionIdByTradeStatus = """
        select min(TransactionId) as "Value" 
        from fund_transaction 
        where FundId = :fundId 
        and ValueDate = :valueDate 
        and TradeStatus = :tradeStatus
        allow filtering;
        """;

    public const string GetFundOrder = """
        SELECT 
           fundId AS "FundId", 
           orderId AS "OrderId", 
           orderDate AS "OrderDate", 
           orderStatus AS "OrderStatus", 
           baseContractId AS "BaseContractId", 
           tradeDate AS "TradeDate", 
           maturityDate AS "MaturityDate", 
           reference AS "Reference", 
           createdOn AS "CreatedOn", 
           createdBy AS "CreatedBy", 
           updatedOn AS "UpdatedOn", 
           updatedBy AS "UpdatedBy" 
        FROM fund_order 
        WHERE fundId = :fundId AND orderId = :orderId;
        """;

    public const string GetFundOrders = """
        SELECT 
           fundId AS "FundId", 
           orderId AS "OrderId", 
           orderDate AS "OrderDate", 
           orderStatus AS "OrderStatus", 
           baseContractId AS "BaseContractId", 
           tradeDate AS "TradeDate", 
           maturityDate AS "MaturityDate", 
           reference AS "Reference", 
           createdOn AS "CreatedOn", 
           createdBy AS "CreatedBy", 
           updatedOn AS "UpdatedOn", 
           updatedBy AS "UpdatedBy" 
        FROM fund_order
        """;

    public const string GetFundOrderTrades = """
        SELECT 
           fundId AS "FundId", 
           orderId AS "OrderId", 
           tradeId AS "TradeId", 
           tradeType AS "TradeType", 
           tradeDate AS "TradeDate", 
           maturityDate AS "MaturityDate", 
           tradeState AS "TradeState", 
           tradeAction AS "TradeAction", 
           reference AS "Reference", 
           primaryTrade AS "PrimaryTrade", 
           baseContractSymbol AS "BaseContractSymbol", 
           createdOn AS "CreatedOn", 
           createdBy AS "CreatedBy", 
           updatedOn AS "UpdatedOn", 
           updatedBy AS "UpdatedBy" 
        FROM fund_order_trade
        """;

    public const string GetFundPnl = """
        select fundId as "FundId", 
        valueDate as "ValueDate", 
        orderId as "OrderId", 
        tradeId as "TradeId",
        tradeType as "TradeType",  
        sum(amount) as "Pnl"
        from fund_transaction
        where FundId = :fundId
        and ValueDate >= :startDate 
        and ValueDate <= :endDate
        and TransactionType = 'RealizedTradePnl'
        group by FundId, ValueDate, OrderId, TradeId, TradeType
        allow filtering;
        """;

    public const string GetFundProfitOrders = """
        select FundId as "FundId", 
        ValueDate as "ValueDate",
        OrderId as "OrderId", 
        sum(Amount) as "Amount"
        from fund_transaction
        where FundId = :fundId 
        and ValueDate >= :startDate 
        and ValueDate <= :endDate
        and TransactionType in ('RealizedTradePnlAdjustment','UnrealizedTradePnl','UnrealizedTradePnlAdjustment','TradeCommission','OpeningTradeAdjustment','RealizedTradePnl','TradeCommissionAdjustment')
        and Amount >= 0
        group by FundId, ValueDate, OrderId 
        allow filtering;
        """;

    public const string GetFunds = """
        SELECT 
           fundid AS "FundId", 
           name AS "Name", 
           description AS "Description", 
           balance AS "Balance", 
           isproduction AS "IsProduction", 
           createdon AS "CreatedOn", 
           createdby AS "CreatedBy" 
        FROM Fund;
        """;

    public const string GetFundTradeCommission = """
        select sum(amount) as "Value"
        from fund_transaction
        where FundId = :fundId
        and ValueDate >= :startDate
        and ValueDate <= :endDate
        and TransactionType = 'TradeCommission'
        group by FundId
        allow filtering;
        """;

    public const string GetFundTransactions = """
           SELECT 
                transactionId AS "TransactionId",
               transactionDate AS "TransactionDate", 
               transactionType AS "TransactionType", 
               fundId AS "FundId", 
               orderId AS "OrderId", 
               tradeId AS "TradeId", 
               tradeType AS "TradeType", 
               valueDate AS "ValueDate", 
               tradeStatus AS "TradeStatus", 
               description AS "Description", 
               amount AS "Amount", 
               balance AS "Balance"
            FROM fund_transaction
            WHERE fundId = :fundId 
            AND valueDate >= :startDate 
            AND valueDate <= :endDate;
          """;

    public const string GetFundTransactionsAll = """
           SELECT 
                transactionId AS "TransactionId",
               transactionDate AS "TransactionDate", 
               transactionType AS "TransactionType", 
               fundId AS "FundId", 
               orderId AS "OrderId", 
               tradeId AS "TradeId", 
               tradeType AS "TradeType", 
               valueDate AS "ValueDate", 
               tradeStatus AS "TradeStatus", 
               description AS "Description", 
               amount AS "Amount", 
               balance AS "Balance"
            FROM fund_transaction
          """;


    public const string InsertFund = """
        INSERT INTO fund (fundId, name, description, balance, isProduction, createdOn, createdBy) VALUES (:fundId, :name, :description, :balance, :isProduction, :createdOn, :createdBy)
        """;

    public const string InsertFundOrder = """
        INSERT INTO fund_order(
           fundId, 
           orderId, 
           orderDate, 
           orderStatus, 
           baseContractId, 
           tradeDate, 
           maturityDate, 
           reference, 
           createdOn, 
           createdBy, 
           updatedOn, 
           updatedBy
        ) VALUES (
           :fundId, 
           :orderId, 
           :orderDate, 
           :orderStatus, 
           :baseContractId, 
           :tradeDate, 
           :maturityDate, 
           :reference, 
           :createdOn, 
           :createdBy, 
           :updatedOn, 
           :updatedBy
        );
        """;

    public const string InsertFundOrderTrade = """
        INSERT INTO fund_order_trade (
           fundId, 
           orderId, 
           tradeId, 
           tradeType, 
           tradeDate, 
           maturityDate, 
           tradeState, 
           tradeAction, 
           reference, 
           primaryTrade, 
           baseContractSymbol, 
           createdOn, 
           createdBy, 
           updatedOn, 
           updatedBy
        ) VALUES (
           :fundId, 
           :orderId, 
           :tradeId, 
           :tradeType, 
           :tradeDate, 
           :maturityDate, 
           :tradeState, 
           :tradeAction, 
           :reference, 
           :primaryTrade, 
           :baseContractSymbol, 
           :createdOn, 
           :createdBy, 
           :updatedOn, 
           :updatedBy
        );
        """;

    public const string InsertFundTransaction = """
        INSERT INTO fund_transaction (
           transactionId,
           transactionDate,
           transactionType,
           fundId,
           orderId,
           tradeId,
           tradeType,
           valueDate,
           tradeStatus,
           description,
           amount,
           balance
        ) VALUES (
           :transactionId,
           :transactionDate,
           :transactionType,
           :fundId,
           :orderId,
           :tradeId,
           :tradeType,
           :valueDate,
           :tradeStatus,
           :description,
           :amount,
           :balance
        );
        """;

    public const string UpdateFundBalance = """
        UPDATE fund SET balance = :balance WHERE fundId = :fundId;
        """;

    public const string UpdateFundOrderStatus = """
        update fund_order
        set OrderStatus = :orderStatus
        where FundId = :fundId
        and OrderId = :orderId
        """;

    public const string UpdateFundOrderTradeState = """
        update fund_order_trade
        set TradeState = :tradeState,
        UpdatedOn = :updatedOn,
        UpdatedBy = :updatedBy
        where FundId = :fundId 
        and OrderId = :orderId
        and TradeId = :tradeId;
        """;
}
