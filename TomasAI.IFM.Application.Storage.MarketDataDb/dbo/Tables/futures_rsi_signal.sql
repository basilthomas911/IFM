CREATE TABLE [dbo].[futures_rsi_signal] (
    [ContractId]       VARCHAR (64) NOT NULL,
    [ValueDate]        DATE         NOT NULL,
    [Timestamp]        DATETIME     NOT NULL,
    [SignalType]       VARCHAR (32) NOT NULL,
    [Price]            MONEY        NOT NULL,
    [PriceChange]      MONEY        NOT NULL,
    [PriceGain]        MONEY        NOT NULL,
    [PriceLoss]        MONEY        NOT NULL,
    [AveragePriceGain] MONEY        NOT NULL,
    [AveragePriceLoss] MONEY        NOT NULL,
    [RS]               REAL         NOT NULL,
    [RSI]              REAL         NOT NULL,
    [RSIAverage]       REAL         NULL,
    [RSISlope]         REAL         NULL,
    [WindowSize]       INT          NULL,
    CONSTRAINT [PK_futures_rsi_signal] PRIMARY KEY CLUSTERED ([ContractId] ASC, [ValueDate] ASC, [SignalType] ASC, [Timestamp] ASC)
);

