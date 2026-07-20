CREATE TABLE [dbo].[futures_bar_data] (
    [Id]         BIGINT       IDENTITY (1, 1) NOT NULL,
    [ContractId] VARCHAR (64) NOT NULL,
    [Symbol]     VARCHAR (64) NOT NULL,
    [ValueDate]  DATETIME     NOT NULL,
    [OpenPrice]  REAL         NOT NULL,
    [HighPrice]  REAL         NOT NULL,
    [LowPrice]   REAL         NOT NULL,
    [ClosePrice] REAL         NOT NULL,
    [Volume]     INT          NOT NULL,
    CONSTRAINT [PK_futures_bar_data_1] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [NCI_futures_bar_data]
    ON [dbo].[futures_bar_data]([Symbol] ASC, [ValueDate] ASC);

