CREATE TABLE [dbo].[futures_iti_signal] (
    [ContractId]                VARCHAR (32) NOT NULL,
    [ValueDate]                 DATE         NOT NULL,
    [IntrinsicTime]             DATETIME     NOT NULL,
    [SequenceId]                BIGINT       IDENTITY (1, 1) NOT NULL,
    [IntrinsicTimeLength]       REAL         NULL,
    [IntrinsicPrice]            REAL         NOT NULL,
    [IntrinsicTimeTrend]        VARCHAR (32) NOT NULL,
    [IntrinsicTimeMode]         VARCHAR (32) NOT NULL,
    [TrendPrice]                REAL         NULL,
    [TrendExtreme]              REAL         NOT NULL,
    [TrendReversal]             REAL         NULL,
    [Lambda]                    REAL         NOT NULL,
    [TargetDelta]               REAL         NULL,
    [PredictedDelta]            REAL         NULL,
    [TrendDelta]                REAL         NULL,
    [UpTrendTrigger]            REAL         NOT NULL,
    [DownTrendTrigger]          REAL         NOT NULL,
    [FuturesPercentChange]      REAL         NULL,
    [FuturesStdDev]             REAL         NULL,
    [FuturesMDI]                REAL         NULL,
    [FuturesRSI]                REAL         NULL,
    [FuturesRSISlope]           REAL         NULL,
    [FuturesFiftyDMA]           REAL         NULL,
    [FuturesTwoHundredDMA]      REAL         NULL,
    [TradeState]                VARCHAR (32) NULL,
    [UpTrendCoastLineCounter]   INT          NULL,
    [DownTrendCoastLineCounter] INT          NULL,
    CONSTRAINT [PK_futures_iti_signal] PRIMARY KEY CLUSTERED ([ContractId] ASC, [ValueDate] ASC, [IntrinsicTime] ASC, [SequenceId] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_futures_iti_signal]
    ON [dbo].[futures_iti_signal]([SequenceId] ASC);

