CREATE TABLE [dbo].[option_leg] (
    [TradeId]         INT          NOT NULL,
    [ContractId]      VARCHAR (64) NOT NULL,
    [Quantity]        INT          NOT NULL,
    [StrikePrice]     MONEY        NOT NULL,
    [OptionLegType]   VARCHAR (64) NOT NULL,
    [OptionLegAction] VARCHAR (64) NOT NULL,
    [CreatedOn]       DATETIME     NULL,
    [CreatedBy]       VARCHAR (64) NULL,
    [UpdatedOn]       DATETIME     NULL,
    [UpdatedBy]       VARCHAR (64) NULL,
    CONSTRAINT [PK_option_leg] PRIMARY KEY CLUSTERED ([TradeId] ASC, [ContractId] ASC)
);

