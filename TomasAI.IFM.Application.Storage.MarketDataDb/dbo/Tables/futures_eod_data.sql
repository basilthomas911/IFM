CREATE TABLE [dbo].[futures_eod_data] (
    [ContractId]               VARCHAR (64) NOT NULL,
    [ValueDate]                DATE         NOT NULL,
    [OpenPrice]                REAL         NULL,
    [HighPrice]                REAL         NULL,
    [LowPrice]                 REAL         NULL,
    [ClosePrice]               REAL         NULL,
    [Volume]                   INT          NULL,
    [DailyPercentChange]       REAL         NULL,
    [DailyStdDev]              REAL         NULL,
    [DailyStdDevAmount]        REAL         NULL,
    [UpperBand]                REAL         NULL,
    [Mean]                     REAL         NULL,
    [LowerBand]                REAL         NULL,
    [MarketDirection]          VARCHAR (32) NULL,
    [MarketVolatility]         VARCHAR (32) NULL,
    [PriceDirection]           VARCHAR (32) NULL,
    [PriceVolatility]          VARCHAR (32) NULL,
    [MarketDirectionIndicator] REAL         NULL,
    [WindowSize]               INT          NULL,
    CONSTRAINT [PK_futures_eod_data] PRIMARY KEY CLUSTERED ([ContractId] ASC, [ValueDate] ASC)
);

