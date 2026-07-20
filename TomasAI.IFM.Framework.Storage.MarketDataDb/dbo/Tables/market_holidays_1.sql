CREATE TABLE [dbo].[market_holidays] (
    [CurrencyType] CHAR (3)     NOT NULL,
    [HolidayDate]  DATETIME     NOT NULL,
    [Description]  VARCHAR (50) NOT NULL,
    CONSTRAINT [PK_equity_market_holidays] PRIMARY KEY CLUSTERED ([CurrencyType] ASC, [HolidayDate] ASC)
);

