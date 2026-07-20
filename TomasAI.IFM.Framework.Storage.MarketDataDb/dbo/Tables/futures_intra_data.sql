CREATE TABLE [dbo].[futures_intra_data] (
    [SequenceId]            BIGINT       IDENTITY (1, 1) NOT NULL,
    [SequenceDate]          DATETIME     NOT NULL,
    [ContractId]            VARCHAR (64) NOT NULL,
    [ValueDate]             DATE         NOT NULL,
    [OpenPrice]             REAL         NULL,
    [HighPrice]             REAL         NULL,
    [LowPrice]              REAL         NULL,
    [ClosePrice]            REAL         NULL,
    [Volume]                INT          NULL,
    [Size]                  INT          NULL,
    [DailyPercentChange]    REAL         NULL,
    [DailyStdDev]           REAL         NULL,
    [UpperBand]             REAL         NULL,
    [Mean]                  REAL         NULL,
    [LowerBand]             REAL         NULL,
    [MarketTrend]           VARCHAR (32) NULL,
    [MarketVolatility]      VARCHAR (32) NULL,
    [MarketVolatilityTrend] VARCHAR (32) NULL,
    [PutSpreadProbability]  REAL         NULL,
    [PutSpreadStdDev]       REAL         NULL,
    [CallSpreadProbability] REAL         NULL,
    [CallSpreadStdDev]      REAL         NULL,
    [RateOfReturn]          REAL         NULL,
    [NearestPutStrike]      INT          NULL,
    [NearestCallStrike]     INT          NULL,
    CONSTRAINT [PK_futures_intra_data_1] PRIMARY KEY CLUSTERED ([SequenceId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_futures_intra_data]
    ON [dbo].[futures_intra_data]([ValueDate] ASC, [ContractId] ASC);

