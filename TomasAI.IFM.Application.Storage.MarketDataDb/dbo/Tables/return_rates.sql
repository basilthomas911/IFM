CREATE TABLE [dbo].[return_rates] (
    [Symbol]       VARCHAR (50) NOT NULL,
    [ValueDate]    DATE         NOT NULL,
    [RateOfReturn] REAL         NOT NULL,
    CONSTRAINT [PK_return_rates] PRIMARY KEY CLUSTERED ([Symbol] ASC, [ValueDate] ASC)
);

