CREATE TABLE [dbo].[option_leg_data] (
    [TradeId]           INT          NOT NULL,
    [TradeType]         VARCHAR (32) NOT NULL,
    [ValueDate]         DATE         NOT NULL,
    [DaysToExpiry]      INT          NOT NULL,
    [TradeStatus]       VARCHAR (64) NOT NULL,
    [OptionLegId]       VARCHAR (64) NOT NULL,
    [BidPrice]          MONEY        NOT NULL,
    [AskPrice]          MONEY        NOT NULL,
    [ImpliedVolatility] REAL         NOT NULL,
    [Delta]             REAL         NOT NULL,
    [Gamma]             REAL         NOT NULL,
    [Theta]             REAL         NOT NULL,
    [Vega]              REAL         NOT NULL,
    [Rho]               REAL         NOT NULL,
    [CreatedOn]         DATETIME     NULL,
    [CreatedBy]         VARCHAR (64) NULL,
    [UpdatedOn]         DATETIME     NULL,
    [UpdatedBy]         VARCHAR (64) NULL,
    CONSTRAINT [PK_option_leg_data] PRIMARY KEY CLUSTERED ([TradeId] ASC, [TradeType] ASC, [ValueDate] ASC, [DaysToExpiry] ASC, [TradeStatus] ASC, [OptionLegId] ASC)
);

