CREATE TABLE [dbo].[fund_transaction] (
    [TransactionId]   INT            IDENTITY (1, 1) NOT NULL,
    [TransactionDate] DATETIME       NOT NULL,
    [TransactionType] VARCHAR (32)   NOT NULL,
    [FundId]          INT            NOT NULL,
    [OrderId]         INT            NOT NULL,
    [TradeId]         INT            NOT NULL,
    [TradeType]       VARCHAR (64)   NOT NULL,
    [ValueDate]       DATE           NOT NULL,
    [TradeStatus]     VARCHAR (32)   NOT NULL,
    [Description]     VARCHAR (4000) NULL,
    [Amount]          MONEY          NOT NULL,
    [Balance]         MONEY          NOT NULL,
    CONSTRAINT [PK_fund_transaction] PRIMARY KEY CLUSTERED ([TransactionId] ASC)
);

