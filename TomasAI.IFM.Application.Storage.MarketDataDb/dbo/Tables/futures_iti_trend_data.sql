CREATE TABLE [dbo].[futures_iti_trend_data] (
    [Symbol]               VARCHAR (10) NOT NULL,
    [ValueDate]            DATE         NOT NULL,
    [Timestamp]            DATETIME     NOT NULL,
    [SequenceId]           BIGINT       IDENTITY (1, 1) NOT NULL,
    [TrendDelta]           REAL         NOT NULL,
    [TargetDelta]          REAL         NULL,
    [TrendDirection]       REAL         NOT NULL,
    [TrendDirectionMode]   REAL         NULL,
    [FuturesPrice]         REAL         NOT NULL,
    [TrendExtreme]         REAL         NULL,
    [FuturesMDI]           REAL         NOT NULL,
    [FuturesStdDev]        REAL         NULL,
    [FuturesRSI]           REAL         NULL,
    [FuturesFiftyDMA]      REAL         NULL,
    [FuturesTwoHundredDMA] REAL         NULL,
    CONSTRAINT [PK_futures_iti_trend_interval_data] PRIMARY KEY CLUSTERED ([Symbol] ASC, [ValueDate] ASC, [Timestamp] ASC, [SequenceId] ASC)
);

