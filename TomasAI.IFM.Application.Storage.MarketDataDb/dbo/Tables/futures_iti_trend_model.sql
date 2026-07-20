CREATE TABLE [dbo].[futures_iti_trend_model] (
    [Symbol]               VARCHAR (10)    NOT NULL,
    [ValueDate]            DATE            NOT NULL,
    [StartDate]            DATETIME        NOT NULL,
    [EndDate]              DATETIME        NOT NULL,
    [Count]                INT             NOT NULL,
    [Maximum]              REAL            NOT NULL,
    [Mean]                 REAL            NOT NULL,
    [Median]               REAL            NOT NULL,
    [Minimum]              REAL            NOT NULL,
    [Skewness]             REAL            NOT NULL,
    [StdDev]               REAL            NOT NULL,
    [Variance]             REAL            NOT NULL,
    [MeanAbsoluteError]    REAL            NOT NULL,
    [MeanSquaredError]     REAL            NOT NULL,
    [RootMeanSquaredError] REAL            NOT NULL,
    [LossFunction]         REAL            NOT NULL,
    [RSquared]             REAL            NOT NULL,
    [ModelData]            VARBINARY (MAX) NOT NULL,
    CONSTRAINT [PK_futures_iti_trend_model] PRIMARY KEY CLUSTERED ([Symbol] ASC, [ValueDate] ASC)
);

