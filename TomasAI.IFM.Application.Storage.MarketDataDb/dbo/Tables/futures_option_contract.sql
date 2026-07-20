CREATE TABLE [dbo].[futures_option_contract] (
    [ContractId]    VARCHAR (64)  NOT NULL,
    [Description]   VARCHAR (256) NULL,
    [Symbol]        VARCHAR (64)  NOT NULL,
    [LocalSymbol]   VARCHAR (64)  NOT NULL,
    [SecurityType]  VARCHAR (64)  NOT NULL,
    [Currency]      VARCHAR (10)  NOT NULL,
    [Exchange]      VARCHAR (64)  NOT NULL,
    [Multiplier]    VARCHAR (10)  NOT NULL,
    [ContractMonth] DATE          NOT NULL,
    [StrikePrice]   REAL          NOT NULL,
    [OptionType]    VARCHAR (64)  NOT NULL,
    CONSTRAINT [PK_futures_option_contract] PRIMARY KEY CLUSTERED ([ContractId] ASC)
);

