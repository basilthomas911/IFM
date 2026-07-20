CREATE TABLE [dbo].[option_pricer_device] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [DeviceId]        INT           NOT NULL,
    [DeviceName]      VARCHAR (255) NOT NULL,
    [SpreadPaths]     INT           NULL,
    [VolatilityPaths] INT           NULL,
    [MaxBatchSize]    INT           NULL,
    [OptionType]      VARCHAR (32)  NULL,
    [Enabled]         BIT           NOT NULL,
    CONSTRAINT [PK_option_pricer_device] PRIMARY KEY CLUSTERED ([Id] ASC)
);

