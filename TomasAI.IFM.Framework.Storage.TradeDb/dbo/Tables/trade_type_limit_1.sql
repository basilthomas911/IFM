CREATE TABLE [dbo].[trade_type_limit] (
    [TradeId]        INT          NOT NULL,
    [TradeType]      VARCHAR (32) NOT NULL,
    [MaxLossLimit]   REAL         NOT NULL,
    [MinProfitLimit] REAL         NOT NULL,
    CONSTRAINT [PK_trade_type_limit] PRIMARY KEY CLUSTERED ([TradeId] ASC, [TradeType] ASC)
);

