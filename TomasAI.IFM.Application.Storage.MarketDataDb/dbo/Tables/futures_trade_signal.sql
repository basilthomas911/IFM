CREATE TABLE [dbo].[futures_trade_signal] (
    [ContractId]          VARCHAR (64) NOT NULL,
    [ValueDate]           DATE         NOT NULL,
    [SequenceId]          INT          IDENTITY (1, 1) NOT NULL,
    [StdDev]              REAL         NOT NULL,
    [FuturesPrice]        REAL         NOT NULL,
    [PriceChangePercent]  REAL         NULL,
    [FundRiskPercent]     REAL         NOT NULL,
    [RSI]                 REAL         NULL,
    [RSISlope]            REAL         NULL,
    [TrendType]           VARCHAR (32) NOT NULL,
    [TrendStrength]       VARCHAR (32) NOT NULL,
    [TradeSignal]         VARCHAR (32) NOT NULL,
    [TDI]                 VARCHAR (32) NOT NULL,
    [TDIStrength]         VARCHAR (32) NULL,
    [MDI]                 REAL         NULL,
    [MDIWatermark]        REAL         NULL,
    [UpTrendingTrigger]   REAL         NULL,
    [DownTrendingTrigger] REAL         NULL,
    [EntryTrigger]        REAL         NULL,
    [ExitTrigger]         REAL         NULL,
    [TrendDelta]          REAL         NULL,
    [TrendExtreme]        REAL         NULL,
    [TrendReversal]       REAL         NULL,
    [FiftyDMA]            REAL         NULL,
    [TwoHundredDMA]       REAL         NULL,
    [TradeExecuteState]   VARCHAR (32) NULL,
    [CreatedOn]           DATETIME     NOT NULL,
    [CreatedBy]           VARCHAR (64) NULL,
    CONSTRAINT [PK_futures_trade_signal] PRIMARY KEY CLUSTERED ([ContractId] ASC, [ValueDate] ASC, [SequenceId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_futures_trade_signal]
    ON [dbo].[futures_trade_signal]([ContractId] ASC, [ValueDate] ASC);

