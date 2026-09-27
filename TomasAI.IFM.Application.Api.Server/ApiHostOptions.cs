namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Controls which responsibilities are hosted by an API-server process.</summary>
public sealed record ApiHostOptions
{
    public const string SectionName = "ApiHost";

    public ApiHostRole Role { get; init; } = ApiHostRole.Combined;

    public bool HostsGateway => Role is ApiHostRole.Gateway or ApiHostRole.Combined;

    public bool HostsRuntime => Role is ApiHostRole.Runtime or ApiHostRole.Combined;

    public ApiHostOptions Validate()
    {
        if (!Enum.IsDefined(Role))
            throw new InvalidOperationException($"Unknown API host role '{Role}'.");
        return this;
    }
}

public enum ApiHostRole
{
    Combined = 0,
    Gateway = 1,
    Runtime = 2
}
