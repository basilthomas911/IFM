CREATE TABLE [dbo].[event_type] (
    [EventTypeId]   BIGINT        IDENTITY (1, 1) NOT NULL,
    [EventTypeName] VARCHAR (255) NOT NULL,
    CONSTRAINT [PK_event_type] PRIMARY KEY CLUSTERED ([EventTypeId] ASC),
    CONSTRAINT [IX_event_type] UNIQUE NONCLUSTERED ([EventTypeName] ASC)
);

