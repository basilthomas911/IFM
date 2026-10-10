using Microsoft.AspNetCore.OutputCaching;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.OutputCaching;

internal static class ApiOutputCachePolicies
{
    internal const string HealthSnapshot = nameof(HealthSnapshot);
    internal const string OperationalSnapshot = nameof(OperationalSnapshot);

    internal static readonly TimeSpan HealthLifetime = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan OperationalLifetime = TimeSpan.FromSeconds(1);
}

/// <summary>Caches successful and unavailable health responses briefly to absorb probe storms.</summary>
internal sealed class HealthOutputCachePolicy : IOutputCachePolicy
{
    /// <inheritdoc />
    public ValueTask CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        var request = context.HttpContext.Request;
        var eligible = (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && !request.Headers.ContainsKey("Authorization");
        context.EnableOutputCaching = eligible;
        context.AllowCacheLookup = eligible;
        context.AllowCacheStorage = eligible;
        context.AllowLocking = true;
        context.CacheVaryByRules.QueryKeys = "*";
        context.ResponseExpirationTimeSpan = ApiOutputCachePolicies.HealthLifetime;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask ServeResponseAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        context.AllowCacheStorage = response.StatusCode is StatusCodes.Status200OK
            or StatusCodes.Status503ServiceUnavailable;
        return ValueTask.CompletedTask;
    }
}
