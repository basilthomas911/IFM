CREATE TABLE [dbo].[entity_type] (
    [EntityTypeId]   BIGINT        IDENTITY (1, 1) NOT NULL,
    [EntityTypeName] VARCHAR (255) NOT NULL,
    CONSTRAINT [PK_event_source_type_id] PRIMARY KEY CLUSTERED ([EntityTypeId] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_entity_type_name]
    ON [dbo].[entity_type]([EntityTypeName] ASC);

