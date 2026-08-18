using System.Globalization;
using System.Text.Json;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed record G2DatabaseIdentity(string ConfigurationKey, string DatabaseName);

public sealed class G2Configuration
{
    const string DefaultFundFixtureName = "IFM G2 Automation Fund";

    public required G0Configuration Process { get; init; }
    public required string RunPrefix { get; init; }
    public required DateOnly ImportDate { get; init; }
    public required string[] ImportCountryCodes { get; init; }
    public required string FundFixtureName { get; init; }
    public required string SecuritiesSymbol { get; init; }
    public required DateOnly SecuritiesMaturityDate { get; init; }
    public required int SecuritiesOptionStrike { get; init; }
    public required string BackupDestinationRoot { get; init; }
    public required string ServerConfigurationPath { get; init; }
    public required G2DatabaseIdentity[] DatabaseIdentities { get; init; }

    public static G2Configuration Load()
    {
        var process = G0Configuration.Load();
        var serverConfigurationPath = Path.Combine(
            process.RepositoryRoot,
            "TomasAI.IFM.Application.Api.Server",
            $"appsettings.{process.EnvironmentName}.json");
        var prefixToken = process.RunId.Split('-', StringSplitOptions.RemoveEmptyEntries).Last()[..8];
        var fixtureSeed = Convert.ToUInt32(prefixToken, 16);
        var fixtureDate = new DateOnly(
            2036 + (int)(fixtureSeed % 20),
            1 + (int)((fixtureSeed >> 8) % 12),
            1 + (int)((fixtureSeed >> 16) % 28));

        return new G2Configuration
        {
            Process = process,
            RunPrefix = Read("IFM_G2_RUN_PREFIX", $"G2-{prefixToken.ToUpperInvariant()}"),
            ImportDate = ReadDate("IFM_G2_IMPORT_DATE", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))),
            ImportCountryCodes = ReadList("IFM_G2_COUNTRY_CODES", ["US"]),
            FundFixtureName = Read("IFM_G2_FUND_NAME", DefaultFundFixtureName),
            SecuritiesSymbol = Read("IFM_G2_SECURITIES_SYMBOL", "ES").ToUpperInvariant(),
            SecuritiesMaturityDate = ReadDate("IFM_G2_SECURITIES_DATE", fixtureDate),
            SecuritiesOptionStrike = ReadInt(
                "IFM_G2_SECURITIES_STRIKE",
                1_000 + (int)((fixtureSeed >> 4) % 8_000)),
            BackupDestinationRoot = Path.GetFullPath(Read(
                "IFM_G2_BACKUP_ROOT",
                Path.Combine(process.ResultsRoot, "G2BackupArtifacts", process.RunId))),
            ServerConfigurationPath = serverConfigurationPath,
            DatabaseIdentities = ReadDatabaseIdentities(serverConfigurationPath)
        };
    }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. Process.Validate()];
        if (string.IsNullOrWhiteSpace(RunPrefix) || RunPrefix.Length > 32
            || RunPrefix.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            errors.Add("G2 run prefix must contain 1-32 ASCII letters, digits, or hyphens.");
        if (ImportDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            errors.Add("G2 import date must precede the current UTC date so desktop startup imports cannot alter its baseline.");
        if (ImportCountryCodes.Length == 0
            || ImportCountryCodes.Any(code => code.Length is < 2 or > 3 || code.Any(character => !char.IsAsciiLetter(character))))
            errors.Add("G2 import country codes must contain one or more two- or three-letter ASCII codes.");
        if (string.IsNullOrWhiteSpace(FundFixtureName))
            errors.Add("G2 designated fund fixture name is required.");
        if (SecuritiesSymbol.Length is < 1 or > 4
            || SecuritiesSymbol.Any(character => !char.IsAsciiLetterOrDigit(character)))
            errors.Add("G2 securities fixture symbol must contain 1-4 ASCII letters or digits.");
        if (SecuritiesMaturityDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)))
            errors.Add("G2 securities fixture date must be more than one year in the future.");
        if (SecuritiesOptionStrike <= 0)
            errors.Add("G2 securities fixture strike must be positive.");
        if (!File.Exists(ServerConfigurationPath))
            errors.Add($"G2 server configuration does not exist: {ServerConfigurationPath}");
        if (DatabaseIdentities.Length == 0)
            errors.Add("G2 could not resolve any database identities from the Development server configuration.");
        foreach (var identity in DatabaseIdentities)
        {
            if (!IsTestDatabase(identity.DatabaseName))
                errors.Add(
                    $"G2 database '{identity.ConfigurationKey}' resolves to non-test identity '{identity.DatabaseName}'.");
        }
        if (!IsDescendantOf(BackupDestinationRoot, Process.ResultsRoot))
            errors.Add("G2 backup destination must be contained by the configured ignored test-results root.");
        return errors;
    }

    public object ToSafeEvidence()
        => new
        {
            Process.EnvironmentName,
            RunPrefix,
            ImportDate,
            ImportCountryCodes,
            FundFixtureName,
            SecuritiesSymbol,
            SecuritiesMaturityDate,
            SecuritiesOptionStrike,
            SecuritiesFuturesContractId,
            SecuritiesOptionContractId,
            BackupDestinationRoot,
            ServerConfigurationPath,
            DatabaseIdentities,
            Process.FmpAdapter,
            Process.FmpCredentialPresent,
            Process.DeterministicAdapterApproved
        };

    public string SecuritiesFuturesContractId
        => $"{SecuritiesSymbol}{SecuritiesMaturityDate:yyyyMMdd}";

    public string SecuritiesOptionContractId
        => $"{SecuritiesSymbol}{SecuritiesMaturityDate:yyyyMMdd}C{SecuritiesOptionStrike}";

    static G2DatabaseIdentity[] ReadDatabaseIdentities(string path)
    {
        if (!File.Exists(path))
            return [];

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
            || connectionStrings.ValueKind != JsonValueKind.Object)
            return [];

        List<G2DatabaseIdentity> identities = [];
        foreach (var property in connectionStrings.EnumerateObject())
        {
            var connectionString = property.Value.GetString() ?? string.Empty;
            var identity = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length == 2
                                && (parts[0].Equals("Database", StringComparison.OrdinalIgnoreCase)
                                    || parts[0].Equals("Default Keyspace", StringComparison.OrdinalIgnoreCase)))
                .Select(parts => parts[1])
                .SingleOrDefault();
            if (!string.IsNullOrWhiteSpace(identity))
                identities.Add(new G2DatabaseIdentity(property.Name, identity));
        }
        return [.. identities];
    }

    static bool IsTestDatabase(string value)
        => value.Contains("test", StringComparison.OrdinalIgnoreCase)
           && !value.Contains("prod", StringComparison.OrdinalIgnoreCase);

    static bool IsDescendantOf(string candidate, string root)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        return !Path.IsPathRooted(relative)
               && !relative.Equals("..", StringComparison.Ordinal)
               && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    static DateOnly ReadDate(string variable, DateOnly defaultValue)
        => DateOnly.TryParseExact(
            Environment.GetEnvironmentVariable(variable),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var value)
            ? value
            : defaultValue;

    static string[] ReadList(string variable, string[] defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    static int ReadInt(string variable, int defaultValue)
        => int.TryParse(
            Environment.GetEnvironmentVariable(variable),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : defaultValue;

    static string Read(string variable, string defaultValue)
        => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable))
            ? defaultValue
            : Environment.GetEnvironmentVariable(variable)!;
}
