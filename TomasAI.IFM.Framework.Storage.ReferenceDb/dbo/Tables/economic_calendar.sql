CREATE TABLE [dbo].[economic_calendar] (
    [EventDate]   DATE          NOT NULL,
    [EventTime]   DATETIME      NOT NULL,
    [CountryCode] CHAR (3)      NOT NULL,
    [EventName]   VARCHAR (255) NOT NULL,
    [Description] TEXT          NULL,
    [Period]      VARCHAR (64)  NOT NULL,
    [Consensus]   FLOAT (53)    NULL,
    [Actual]      FLOAT (53)    NULL,
    [Prior]       FLOAT (53)    NULL,
    [CreatedOn]   DATETIME      NOT NULL,
    [CreatedBy]   VARCHAR (255) NULL,
    CONSTRAINT [PK_economic_calendar] PRIMARY KEY CLUSTERED ([EventDate] ASC, [CountryCode] ASC, [EventName] ASC)
);

