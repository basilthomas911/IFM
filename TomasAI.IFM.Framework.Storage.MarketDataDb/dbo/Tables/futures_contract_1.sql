CREATE TABLE [dbo].[futures_contract] (
    [ContractId]      VARCHAR (64)  NOT NULL,
    [Description]     VARCHAR (256) NOT NULL,
    [Symbol]          VARCHAR (64)  NOT NULL,
    [LocalSymbol]     VARCHAR (10)  NOT NULL,
    [SecurityType]    VARCHAR (10)  NOT NULL,
    [Currency]        VARCHAR (10)  NOT NULL,
    [Exchange]        VARCHAR (10)  NOT NULL,
    [Multiplier]      VARCHAR (10)  NOT NULL,
    [LastTradeDate]   DATE          NOT NULL,
    [CurrentlyTraded] BIT           NOT NULL,
    CONSTRAINT [PK_futures_contract] PRIMARY KEY CLUSTERED ([ContractId] ASC)
);

