CREATE TABLE [dbo].[event_source] (
    [EventSourceId]      BIGINT   IDENTITY (1, 1) NOT NULL,
    [EntityId]           BIGINT   NOT NULL,
    [EntityTypeId]       BIGINT   NOT NULL,
    [EventSourceVersion] BIGINT   NOT NULL,
    [EventSourceDate]    DATETIME NOT NULL,
    CONSTRAINT [PK_event_source_1] PRIMARY KEY CLUSTERED ([EventSourceId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_event_source]
    ON [dbo].[event_source]([EntityId] ASC, [EntityTypeId] ASC);

