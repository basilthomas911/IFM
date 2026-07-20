CREATE TABLE [dbo].[futures_trade_signal_metrics_llm] (
    [ContractId]               VARCHAR (32) NOT NULL,
    [ValueDate]                DATE         NOT NULL,
    [Timestamp]                DATETIME     NOT NULL,
    [MarketDirection]          VARCHAR (32) NULL,
    [MarketVolatility]         VARCHAR (32) NULL,
    [PriceDirection]           VARCHAR (32) NULL,
    [PriceVolatility]          VARCHAR (32) NULL,
    [MarketDirectionIndicator] REAL         NULL,
    [CreatedOn]                DATETIME     NULL,
    [CreatedBy]                VARCHAR (64) NULL,
    CONSTRAINT [PK_futures_trade_signal_metrics_llm] PRIMARY KEY CLUSTERED ([ContractId] ASC, [ValueDate] ASC, [Timestamp] ASC)
);

