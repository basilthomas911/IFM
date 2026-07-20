CREATE TABLE [dbo].[futures_tick_data_old] (
    [ContractId] VARCHAR (64) NOT NULL,
    [TickDate]   DATETIME     NOT NULL,
    [TickTime]   BIGINT       NOT NULL,
    [Price]      REAL         NOT NULL,
    [Size]       INT          NOT NULL,
    [ValueDate]  DATE         NULL,
    CONSTRAINT [PK_futures_tick_data] PRIMARY KEY CLUSTERED ([ContractId] ASC, [TickDate] ASC, [TickTime] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_futures_tick_data]
    ON [dbo].[futures_tick_data_old]([ContractId] ASC, [TickDate] ASC);

