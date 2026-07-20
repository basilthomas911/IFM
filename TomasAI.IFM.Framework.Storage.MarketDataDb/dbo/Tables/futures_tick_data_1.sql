CREATE TABLE [dbo].[futures_tick_data] (
    [ContractId] VARCHAR (64) NOT NULL,
    [TickDate]   DATETIME     NOT NULL,
    [TickTime]   BIGINT       NOT NULL,
    [Price]      REAL         NOT NULL,
    [Size]       INT          NOT NULL,
    [ValueDate]  DATE         NOT NULL,
    CONSTRAINT [PK_futures_tick_data1] PRIMARY KEY CLUSTERED ([ContractId] ASC, [TickTime] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_futures_tick_data_1]
    ON [dbo].[futures_tick_data]([ContractId] ASC, [TickTime] ASC, [ValueDate] ASC);

