using System.Diagnostics;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Hands off only after acknowledged UI publication. The external manager owns termination and the deadline.</summary>
public sealed class ApiRecoveryRestartEscalation(IMarketDataApi market, IFuturesMarketSessionAuthority sessions)
{
    public async Task RequestAsync(MarketDataDatasetRecoveryFailedEvent failure, CancellationToken token)
    {
        var directory = Environment.GetEnvironmentVariable(ApiRecoveryRestartProtocol.DirectoryVariable);
        var sessionId = Environment.GetEnvironmentVariable(ApiRecoveryRestartProtocol.SessionVariable);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(sessionId)
            || Environment.GetEnvironmentVariable(ApiRecoveryRestartProtocol.ControllerVariable) != "1") return;
        var session = sessions.Current;
        if (failure.Role != "Options" || session.ActiveValueDate != failure.EntityId.ValueDate
            || session.SessionEndUtc <= DateTime.UtcNow) return;
        var path = ApiRecoveryRestartProtocol.RequestPath(directory, failure.CommandId);
        if (File.Exists(path) || File.Exists(ApiRecoveryRestartProtocol.ResultPath(directory, failure.CommandId))) return;
        var saved = await market.ReadTradeFeedRecoveryAsync(failure.EntityId.ValueDate, token).WaitAsync(token);
        if (saved.Length == 0) return;
        using var process = Process.GetCurrentProcess();
        var recoveryDeadline = DateTime.TryParse(failure.ErrorData, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed.ToUniversalTime() : failure.ErrorDate;
        var request = new ApiRecoveryRestartRequest(failure.CommandId, sessionId, process.Id, process.StartTime.ToUniversalTime(),
            failure.EntityId.ValueDate, session.SessionEndUtc, DateTime.UtcNow, saved.Select(row => row.TradeId).Distinct().ToArray(), recoveryDeadline);
        Directory.CreateDirectory(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(request), token);
            File.Move(temporary, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path)) { }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
