CREATE TABLE [dbo].[futures_bar_data] (
    [ContractId]       VARCHAR (64) NOT NULL,
    [Symbol]           VARCHAR (64) NOT NULL,
    [ValueDate]        DATE         NOT NULL,
    [BarDate]          DATETIME     NOT NULL,
    [BarRateType]      VARCHAR (50) NOT NULL,
    [BarValue]         MONEY        NOT NULL,
    [UpTrendTrigger]   REAL         NULL,
    [DownTrendTrigger] REAL         NULL,
    CONSTRAINT [PK_futures_bar_data] PRIMARY KEY CLUSTERED ([ContractId] ASC, [Symbol] ASC, [ValueDate] ASC, [BarDate] ASC)
);


GO
CREATE NONCLUSTERED INDEX [NCI_futures_bar_data]
    ON [dbo].[futures_bar_data]([Symbol] ASC, [ValueDate] ASC);

