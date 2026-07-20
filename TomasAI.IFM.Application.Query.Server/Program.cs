using Serilog;

namespace TomasAI.IFM.Application.Query.Server;

public class Program
{
    public static void Main(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.ConfigureWebApp(out var logger);
            builder.Services.RegisterServices(builder.Configuration, logger);
            var app = builder.Build();
            app.ConfigureRequestPipeline(logger);
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "IFM QueryServer: start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
