CREATE TABLE [dbo].[futures_trade_signal_llm] (
    [ContractId]         VARCHAR (32) NOT NULL,
    [ValueDate]          DATE         NOT NULL,
    [Timestamp]          DATETIME     NOT NULL,
    [OpenPrice]          REAL         NOT NULL,
    [HighPrice]          REAL         NOT NULL,
    [LowPrice]           REAL         NOT NULL,
    [ClosePrice]         REAL         NOT NULL,
    [Volume]             INT          NOT NULL,
    [DailyPercentChange] REAL         NOT NULL,
    [DailyStdDev]        REAL         NOT NULL,
    [UpperBand]          REAL         NOT NULL,
    [Mean]               REAL         NOT NULL,
    [LowerBand]          REAL         NOT NULL,
    [PriceVolatility]    REAL         NOT NULL,
    [CreatedOn]          DATETIME     NULL,
    [CreatedBy]          VARCHAR (64) NULL,
    CONSTRAINT [PK_futures_trade_signal_llm] PRIMARY KEY CLUSTERED ([ContractId] ASC, [ValueDate] ASC, [Timestamp] ASC)
);

