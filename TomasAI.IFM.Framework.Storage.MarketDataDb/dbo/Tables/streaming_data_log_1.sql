CREATE TABLE [dbo].[streaming_data_log] (
    [ValueDate]    DATETIME       NOT NULL,
    [ErrorCode]    INT            NOT NULL,
    [ErrorMessage] VARCHAR (4000) NOT NULL
);

