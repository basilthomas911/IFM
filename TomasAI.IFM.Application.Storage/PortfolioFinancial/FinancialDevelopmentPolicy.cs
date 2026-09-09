namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>
/// Trusted host policy for synthetic opening capital. The API composition root supplies the
/// host environment; a command's principal, roles or execution-environment text cannot enable it.
/// A missing policy denies development funding.
/// </summary>
public sealed record FinancialDevelopmentPolicy(bool IsDevelopmentEnvironment = false);
