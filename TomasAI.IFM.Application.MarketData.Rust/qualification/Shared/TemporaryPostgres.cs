using System.Diagnostics;
using System.Text.Json;
using Npgsql;

/// <summary>Qualification-only owner of one named, ephemeral PostgreSQL container.
/// The application database and its credentials are never used by this fixture.</summary>
sealed class TemporaryPostgres:IAsyncDisposable
{
    readonly string name="ifm-native-pg-"+Guid.NewGuid().ToString("N");
    readonly Dictionary<string,string?> previous=new();
    bool started;
    public string ConnectionString{get;private set;}="";
    public static async Task<TemporaryPostgres> CreateAsync(string schema)
    {
        var owner=new TemporaryPostgres();
        try{await owner.StartAsync(schema);return owner;}catch{await owner.DisposeAsync();throw;}
    }
    async Task StartAsync(string schema)
    {
        void Set(string key,string value){previous[key]=Environment.GetEnvironmentVariable(key);Environment.SetEnvironmentVariable(key,value);}
        Set("DOTNET_ENVIRONMENT","Test");Set("ASPNETCORE_ENVIRONMENT","Test");Set("POSTGRES_TEST_KEY",JsonSerializer.Serialize(new{userid="postgres",password="native-fixture-only"}));
        started=true;
        await Docker("run","--detach","--rm","--name",name,"--label","ifm.native.fixture="+name,"--publish","127.0.0.1::5432","--env","POSTGRES_PASSWORD=native-fixture-only","--env","POSTGRES_DB=ifm_native_fixture","postgres:17.2");
        var endpoint=(await Docker("port",name,"5432/tcp")).Trim();var port=int.Parse(endpoint[(endpoint.LastIndexOf(':')+1)..]);
        ConnectionString=$"Host=127.0.0.1;Port={port};Database=ifm_native_fixture";
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var connection=new NpgsqlConnection(ConnectionString+";Username=postgres;Password=native-fixture-only;Timeout=2");
        while(true){try{await connection.OpenAsync(deadline.Token);break;}catch(NpgsqlException){await Task.Delay(100,deadline.Token);}}
        await using var command=new NpgsqlCommand(schema,connection);await command.ExecuteNonQueryAsync(deadline.Token);
    }
    public async ValueTask DisposeAsync()
    {
        try{if(started){started=false;NpgsqlConnection.ClearAllPools();await Docker("rm","--force",name);}}
        finally{foreach(var item in previous)Environment.SetEnvironmentVariable(item.Key,item.Value);previous.Clear();}
    }
    static async Task<string> Docker(params string[] arguments)
    {
        var start=new ProcessStartInfo("docker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var value in arguments)start.ArgumentList.Add(value);
        using var process=Process.Start(start)??throw new InvalidOperationException("Docker did not start.");var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try{await process.WaitForExitAsync(deadline.Token);}catch{if(!process.HasExited)process.Kill();await process.WaitForExitAsync();throw;}
        var result=await output;var failure=await errors;if(process.ExitCode!=0)throw new InvalidOperationException("Isolated PostgreSQL fixture failed: "+failure);return result;
    }
}
