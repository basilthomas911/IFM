CREATE TABLE [dbo].[index_contract] (
    [ContractId]   VARCHAR (64)  NOT NULL,
    [Description]  VARCHAR (256) NOT NULL,
    [Symbol]       VARCHAR (64)  NOT NULL,
    [SecurityType] VARCHAR (10)  NOT NULL,
    [Currency]     VARCHAR (10)  NOT NULL,
    [Exchange]     VARCHAR (10)  NOT NULL,
    CONSTRAINT [PK_index_contract] PRIMARY KEY CLUSTERED ([ContractId] ASC)
);

