CREATE TABLE [dbo].[economic_calendar] (
    [EventDate]   DATETIME      NOT NULL,
    [CountryCode] CHAR (3)      NOT NULL,
    [EventName]   VARCHAR (255) NOT NULL,
    [Actual]      VARCHAR (50)  NULL,
    [Forecast]    VARCHAR (50)  NULL,
    [Prior]       VARCHAR (50)  NULL,
    [CreatedOn]   DATETIME      NOT NULL,
    [CreatedBy]   VARCHAR (255) NULL,
    CONSTRAINT [PK_economic_calendar] PRIMARY KEY CLUSTERED ([EventDate] ASC, [CountryCode] ASC, [EventName] ASC)
);

