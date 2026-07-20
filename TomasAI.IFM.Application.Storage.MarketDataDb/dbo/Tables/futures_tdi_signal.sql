CREATE TABLE [dbo].[futures_tdi_signal] (
    [ContractId]     VARCHAR (64) NOT NULL,
    [ValueDate]      DATE         NOT NULL,
    [Timestamp]      DATETIME     NOT NULL,
    [UpTrendCount]   INT          NULL,
    [DownTrendCount] INT          NULL,
    [TDI]            VARCHAR (32) NOT NULL,
    [TDIStrength]    VARCHAR (32) NULL,
    CONSTRAINT [PK_futures_tdi_signal] PRIMARY KEY CLUSTERED ([ContractId] ASC, [ValueDate] ASC, [Timestamp] ASC)
);

