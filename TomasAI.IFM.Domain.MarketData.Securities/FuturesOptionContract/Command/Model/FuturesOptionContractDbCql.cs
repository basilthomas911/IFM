namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

internal static class FuturesOptionContractDbCql
{
	public const string GetFuturesOptionContract = """
		SELECT
			contractId AS "ContractId",
			description AS "Description",
			symbol AS "Symbol",
			localSymbol AS "LocalSymbol",
			securityType AS "SecurityType",
			currency AS "Currency",
			exchange AS "Exchange",
			multiplier AS "Multiplier",
			contractMonth AS "ContractMonth",
			strikePrice AS "StrikePrice",
			optionType AS "OptionType"
		FROM futures_option_contract
		WHERE contractId = :contractId;
		""";

	public const string GetFuturesOptionContractsBySymbol = """
		SELECT
			contractId AS "ContractId",
			description AS "Description",
			symbol AS "Symbol",
			localSymbol AS "LocalSymbol",
			securityType AS "SecurityType",
			currency AS "Currency",
			exchange AS "Exchange",
			multiplier AS "Multiplier",
			contractMonth AS "ContractMonth",
			strikePrice AS "StrikePrice",
			optionType AS "OptionType"
		FROM futures_option_contract
		WHERE symbol = :symbol
		ALLOW FILTERING;
		""";

	public const string GetFuturesOptionContracts = """
		SELECT
			contractId AS "ContractId",
			description AS "Description",
			symbol AS "Symbol",
			localSymbol AS "LocalSymbol",
			securityType AS "SecurityType",
			currency AS "Currency",
			exchange AS "Exchange",
			multiplier AS "Multiplier",
			contractMonth AS "ContractMonth",
			strikePrice AS "StrikePrice",
			optionType AS "OptionType"
		FROM futures_option_contract;
		""";

	public const string InsertFuturesOptionContract = """
		INSERT INTO futures_option_contract (
			contractId,
			description,
			symbol,
			localSymbol,
			securityType,
			currency,
			exchange,
			multiplier,
			contractMonth,
			strikePrice,
			optionType)
		VALUES (
			:contractId,
			:description,
			:symbol,
			:localSymbol,
			:securityType,
			:currency,
			:exchange,
			:multiplier,
			:contractMonth,
			:strikePrice,
			:optionType);
		""";

	public const string DeleteFuturesOptionContractById = """
		DELETE FROM futures_option_contract
		WHERE contractId = :contractId;
		""";
}
