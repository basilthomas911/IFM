CREATE TABLE [dbo].[denormalizer_log] (
    [DenormalizerLogId] BIGINT         IDENTITY (1, 1) NOT NULL,
    [CommandId]         VARCHAR (64)   NOT NULL,
    [DenormalizerDate]  DATETIME       NOT NULL,
    [DenormalizerName]  VARCHAR (4000) NOT NULL,
    [DenormalizerData]  TEXT           NULL,
    [UserName]          VARCHAR (255)  NOT NULL,
    [ErrorMessage]      VARCHAR (255)  NOT NULL,
    [ErrorCode]         INT            NOT NULL,
    [ErrorType]         VARCHAR (64)   NOT NULL,
    [ErrorData]         TEXT           NULL,
    CONSTRAINT [PK_denormalizer_log] PRIMARY KEY CLUSTERED ([DenormalizerLogId] ASC)
);

