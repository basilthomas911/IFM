using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.Application.Storage.FundDb;

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

    public const string GetFundTransactionIdentityV4 = """
        SELECT transactionId AS "TransactionId"
        FROM fund_transaction_identity_v4
        WHERE fundId = :fundId
          AND valueDate = :valueDate
          AND orderId = :orderId
          AND tradeId = :tradeId
          AND tradeType = :tradeType
          AND transactionType = :transactionType
          AND transactionDate = :transactionDate;
        """;

    public const string ReserveFundTransactionIdentityV4 = """
        INSERT INTO fund_transaction_identity_v4 (
            fundId, valueDate, orderId, tradeId, tradeType,
            transactionType, transactionDate, transactionId)
        VALUES (
            :fundId, :valueDate, :orderId, :tradeId, :tradeType,
            :transactionType, :transactionDate, :transactionId)
        IF NOT EXISTS;
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

    public const string DeleteFundTransactionTimelineV3 = """
        DELETE FROM fund_transaction_timeline_v3
        WHERE fundId = :fundId
          AND monthBucket = :monthBucket
          AND valueDate = :valueDate
          AND orderId = :orderId
          AND tradeId = :tradeId
          AND tradeType = :tradeType
          AND transactionType = :transactionType
          AND transactionDate = :transactionDate
          AND transactionId = :transactionId;
        """;

    public const string DeleteFundBalanceByStatusDayV3 = """
        DELETE FROM fund_balance_by_status_day_v3
        WHERE fundId = :fundId
          AND monthBucket = :monthBucket
          AND valueDate = :valueDate
          AND tradeStatus = :tradeStatus
          AND transactionDate = :transactionDate
          AND transactionId = :transactionId
          AND orderId = :orderId
          AND tradeId = :tradeId
          AND tradeType = :tradeType
          AND transactionType = :transactionType;
        """;

    public const string DeleteFundTransactionAmountV3 = """
        DELETE FROM fund_transaction_amount_v3
        WHERE fundId = :fundId
          AND monthBucket = :monthBucket
          AND transactionType = :transactionType
          AND amountSign = :amountSign
          AND valueDate = :valueDate
          AND transactionDate = :transactionDate
          AND transactionId = :transactionId
          AND orderId = :orderId
          AND tradeId = :tradeId
          AND tradeType = :tradeType;
        """;

    public const string DeleteFundTransactionTimelinePartitionV3 = """
        DELETE FROM fund_transaction_timeline_v3
        WHERE fundId = :fundId AND monthBucket = :monthBucket;
        """;

    public const string DeleteFundBalanceByStatusMonthPartitionV3 = """
        DELETE FROM fund_balance_by_status_day_v3
        WHERE fundId = :fundId AND monthBucket = :monthBucket;
        """;

    public const string DeleteFundTransactionAmountPartitionV3 = """
        DELETE FROM fund_transaction_amount_v3
        WHERE fundId = :fundId AND monthBucket = :monthBucket;
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

    // Added from FundDbCql.Designer.cs

    public const string DeleteFund = """
        DELETE FROM fund WHERE fundId = :fundId;
        """;

    public const string DeleteFundOrder = """
        DELETE FROM fund_order WHERE fundId = :fundId AND orderId = :orderId;
        """;

    public const string ReleaseFundOrderWriteOwnershipV3 = """
        DELETE FROM fund_order_write_ownership_v3
        WHERE orderId = :orderId
        IF operationId = :operationId;
        """;

    public const string DeleteFundOrderByOrderIdV3ForOfflineRepair = """
        DELETE FROM fund_order_by_order_id_v3
        WHERE orderId = :orderId;
        """;

    public const string DeleteFundOrderTrade = """
        DELETE FROM fund_order_trade WHERE fundId = :fundId AND orderId = :orderId AND tradeId = :tradeId;
        """;

    public const string GetFundBalance = """
        select balance as "Value"
        from fund
        where fundId = :fundId;
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

    public const string GetFundIdFromOrderId = """
        select FundId as "Value"
        from fund_order_by_order_id_v3
        where orderId = :orderId;
        """;

    public const string GetFundOrderReservationV3 = """
        SELECT fundId, reservationToken
        FROM fund_order_by_order_id_v3
        WHERE orderId = :orderId;
        """;

    public const string GetFundOrderByOrderIdKeysV3All = """
        SELECT orderId AS "OrderId", fundId AS "FundId", reservationToken AS "ReservationToken"
        FROM fund_order_by_order_id_v3;
        """;

    public const string GetFundOrderWriteOwnershipsV3All = """
        SELECT orderId, operationId, startedOn
        FROM fund_order_write_ownership_v3;
        """;

    public const string GetFundTransactionTimelineV3 = """
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
        FROM fund_transaction_timeline_v3
        WHERE fundId = :fundId
          AND monthBucket = :monthBucket
          AND valueDate >= :startDate
          AND valueDate <= :endDate;
        """;

    public const string GetFundTransactionAmountsV3 = """
        SELECT
            fundId AS "FundId",
            valueDate AS "ValueDate",
            orderId AS "OrderId",
            tradeId AS "TradeId",
            tradeType AS "TradeType",
            transactionDate AS "TransactionDate",
            transactionId AS "TransactionId",
            amount AS "Amount"
        FROM fund_transaction_amount_v3
        WHERE fundId = :fundId
          AND monthBucket = :monthBucket
          AND transactionType = :transactionType
          AND amountSign = :amountSign
          AND valueDate >= :startDate
          AND valueDate <= :endDate;
        """;

    public const string GetOpeningFundBalanceV3 = """
        SELECT balance AS "Value"
        FROM fund_balance_by_status_day_v3
        WHERE fundId = :fundId
          AND monthBucket = :monthBucket
          AND valueDate = :valueDate
          AND tradeStatus = :tradeStatus
        ORDER BY transactionDate ASC, transactionId ASC
        LIMIT 1;
        """;

    public const string GetClosingFundBalanceV3 = """
        SELECT balance AS "Value"
        FROM fund_balance_by_status_day_v3
        WHERE fundId = :fundId
          AND monthBucket = :monthBucket
          AND valueDate = :valueDate
          AND tradeStatus = :tradeStatus
        ORDER BY transactionDate DESC, transactionId DESC
        LIMIT 1;
        """;

    public const string GetFundTransactionTimelineKeysV3 = """
        SELECT fundId AS "FundId", valueDate AS "ValueDate", orderId AS "OrderId",
               tradeId AS "TradeId", tradeType AS "TradeType", transactionType AS "TransactionType",
               transactionDate AS "TransactionDate", transactionId AS "TransactionId"
        FROM fund_transaction_timeline_v3
        WHERE fundId = :fundId AND monthBucket = :monthBucket;
        """;

    public const string GetFundBalanceByStatusMonthKeysV3 = """
        SELECT fundId AS "FundId", valueDate AS "ValueDate", orderId AS "OrderId",
               tradeId AS "TradeId", tradeType AS "TradeType", transactionType AS "TransactionType",
               transactionDate AS "TransactionDate", transactionId AS "TransactionId"
        FROM fund_balance_by_status_day_v3
        WHERE fundId = :fundId AND monthBucket = :monthBucket;
        """;

    public const string GetFundTransactionAmountKeysV3 = """
        SELECT fundId AS "FundId", valueDate AS "ValueDate", orderId AS "OrderId",
               tradeId AS "TradeId", tradeType AS "TradeType", transactionType AS "TransactionType",
               transactionDate AS "TransactionDate", transactionId AS "TransactionId"
        FROM fund_transaction_amount_v3
        WHERE fundId = :fundId AND monthBucket = :monthBucket;
        """;

    public const string GetFundTransactionProjectionStateV3 = """
        SELECT generation AS "Generation", isComplete AS "IsComplete",
               sourceCount AS "SourceCount", sourceFingerprint AS "SourceFingerprint",
               reconciledOn AS "ReconciledOn"
        FROM fund_transaction_projection_state_v3
        WHERE fundId = :fundId AND monthBucket = :monthBucket;
        """;

    public const string MarkFundTransactionProjectionIncompleteV3 = """
        UPDATE fund_transaction_projection_state_v3
        SET generation = :generation, isComplete = false, sourceCount = 0,
            sourceFingerprint = '', reconciledOn = null
        WHERE fundId = :fundId AND monthBucket = :monthBucket;
        """;

    public const string MarkFundTransactionProjectionCompleteV3 = """
        UPDATE fund_transaction_projection_state_v3
        SET isComplete = true, sourceCount = :sourceCount,
            sourceFingerprint = :sourceFingerprint, reconciledOn = :reconciledOn
        WHERE fundId = :fundId AND monthBucket = :monthBucket
        IF generation = :generation;
        """;

    public const string InsertFundTransactionProjectionMutationV3 = """
        INSERT INTO fund_transaction_projection_mutation_v3 (
            fundId, monthBucket, mutationId, startedOn)
        VALUES (:fundId, :monthBucket, :mutationId, :startedOn);
        """;

    public const string DeleteFundTransactionProjectionMutationV3 = """
        DELETE FROM fund_transaction_projection_mutation_v3
        WHERE fundId = :fundId
          AND monthBucket = :monthBucket
          AND mutationId = :mutationId;
        """;

    public const string GetFundTransactionProjectionMutationsV3 = """
        SELECT mutationId AS "MutationId"
        FROM fund_transaction_projection_mutation_v3
        WHERE fundId = :fundId AND monthBucket = :monthBucket;
        """;

    public const string GetFundTransactionProjectionMutationJournalV3All = """
        SELECT fundId AS "FundId", monthBucket AS "MonthBucket",
               mutationId AS "MutationId", startedOn AS "StartedOn"
        FROM fund_transaction_projection_mutation_v3;
        """;

    public const string InsertFundTransactionWriteMutationV3 = """
        INSERT INTO fund_transaction_write_mutation_v3 (
            fundId, mutationId, startedOn)
        VALUES (:fundId, :mutationId, :startedOn);
        """;

    public const string GetFundTransactionWriteMutationsV3 = """
        SELECT mutationId AS "MutationId"
        FROM fund_transaction_write_mutation_v3
        WHERE fundId = :fundId;
        """;

    public const string GetFundTransactionWriteMutationJournalV3 = """
        SELECT fundId AS "FundId", mutationId AS "MutationId", startedOn AS "StartedOn"
        FROM fund_transaction_write_mutation_v3
        WHERE fundId = :fundId;
        """;

    public const string DeleteFundTransactionWriteMutationV3 = """
        DELETE FROM fund_transaction_write_mutation_v3
        WHERE fundId = :fundId AND mutationId = :mutationId;
        """;

    public const string ClaimFundTransactionWriteOwnershipV3 = """
        INSERT INTO fund_transaction_write_ownership_v3 (
            fundId, ownerMutationId, conflicted, claimedOn)
        VALUES (:fundId, :mutationId, false, :claimedOn)
        IF NOT EXISTS;
        """;

    public const string FlagFundTransactionWriteOwnershipConflictV3 = """
        UPDATE fund_transaction_write_ownership_v3
        SET conflicted = true
        WHERE fundId = :fundId
        IF EXISTS;
        """;

    public const string ReleaseFundTransactionWriteOwnershipIfSafeV3 = """
        DELETE FROM fund_transaction_write_ownership_v3
        WHERE fundId = :fundId
        IF ownerMutationId = :mutationId AND conflicted = false;
        """;

    public const string ReleaseFundTransactionWriteOwnershipV3 = """
        DELETE FROM fund_transaction_write_ownership_v3
        WHERE fundId = :fundId
        IF ownerMutationId = :mutationId;
        """;

    public const string GetFirstFundTransactionValueDate = """
        SELECT valueDate AS "Value"
        FROM fund_transaction
        WHERE fundId = :fundId
          AND valueDate >= :startDate
        LIMIT 1;
        """;

    public const string GetLastFundTransactionValueDate = """
        SELECT valueDate AS "Value"
        FROM fund_transaction
        WHERE fundId = :fundId
          AND valueDate <= :endDate
        ORDER BY valueDate DESC
        LIMIT 1;
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

    public const string InsertFundOrderByOrderIdV3 = """
        INSERT INTO fund_order_by_order_id_v3 (orderId, fundId, reservationToken)
        VALUES (:orderId, :fundId, :reservationToken)
        IF NOT EXISTS;
        """;

    public const string ClaimFundOrderWriteOwnershipV3 = """
        INSERT INTO fund_order_write_ownership_v3 (orderId, operationId, startedOn)
        VALUES (:orderId, :operationId, :startedOn)
        IF NOT EXISTS;
        """;

    public const string RotateFundOrderByOrderIdV3Reservation = """
        UPDATE fund_order_by_order_id_v3
        SET reservationToken = :reservationToken
        WHERE orderId = :orderId
        IF fundId = :fundId
        AND reservationToken = :expectedReservationToken;
        """;

    public const string InsertFundTransactionTimelineV3 = """
        INSERT INTO fund_transaction_timeline_v3 (
           fundId,
           monthBucket,
           valueDate,
           transactionDate,
           transactionId,
           transactionType,
           orderId,
           tradeId,
           tradeType,
           tradeStatus,
           description,
           amount,
           balance
        ) VALUES (
           :fundId,
           :monthBucket,
           :valueDate,
           :transactionDate,
           :transactionId,
           :transactionType,
           :orderId,
           :tradeId,
           :tradeType,
           :tradeStatus,
           :description,
           :amount,
           :balance
        );
        """;

    public const string InsertFundBalanceByStatusDayV3 = """
        INSERT INTO fund_balance_by_status_day_v3 (
           fundId,
           monthBucket,
           valueDate,
           tradeStatus,
           transactionDate,
           transactionId,
           transactionType,
           orderId,
           tradeId,
           tradeType,
           balance
        ) VALUES (
           :fundId,
           :monthBucket,
           :valueDate,
           :tradeStatus,
           :transactionDate,
           :transactionId,
           :transactionType,
           :orderId,
           :tradeId,
           :tradeType,
           :balance
        );
        """;

    public const string InsertFundTransactionAmountV3 = """
        INSERT INTO fund_transaction_amount_v3 (
           fundId,
           monthBucket,
           transactionType,
           amountSign,
           valueDate,
           transactionDate,
           transactionId,
           orderId,
           tradeId,
           tradeType,
           amount
        ) VALUES (
           :fundId,
           :monthBucket,
           :transactionType,
           :amountSign,
           :valueDate,
           :transactionDate,
           :transactionId,
           :orderId,
           :tradeId,
           :tradeType,
           :amount
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
