CREATE TABLE [dbo].[yield_curve_rates] (
    [ValueDate]  DATE NOT NULL,
    [OneMonth]   REAL NOT NULL,
    [TwoMonth]   REAL NULL,
    [ThreeMonth] REAL NOT NULL,
    [SixMonth]   REAL NOT NULL,
    [OneYear]    REAL NOT NULL,
    [TwoYear]    REAL NOT NULL,
    [ThreeYear]  REAL NOT NULL,
    [FiveYear]   REAL NOT NULL,
    [SevenYear]  REAL NOT NULL,
    [TenYear]    REAL NOT NULL,
    [TwentyYear] REAL NOT NULL,
    [ThirtyYear] REAL NOT NULL,
    CONSTRAINT [PK_yield_curve_rates] PRIMARY KEY CLUSTERED ([ValueDate] ASC)
);

