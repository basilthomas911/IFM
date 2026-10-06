using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using TomasAI.IFM.Framework.Telemetry.Logging;
using Xunit;

namespace TomasAI.IFM.Framework.Telemetry.UnitTests;

public sealed class OtlpStructuredLogSinkTests
{
    [Fact]
    public async Task Http_export_preserves_typed_arguments_service_and_trace_context()
    {
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var receiver = builder.Build();
        receiver.MapPost("/v1/logs", async context =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer);
            context.Response.StatusCode = 200;
            received.TrySetResult(buffer.ToArray());
        });
        await receiver.StartAsync();
        var endpoint = receiver.Urls.Single() + "/v1/logs";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:Logs:OtlpEndpoint"] = endpoint,
            ["Telemetry:Logs:OtlpProtocol"] = "http/protobuf"
        }).Build();
        using var sink = new OtlpStructuredLogSink(configuration, "IFM.Logging.IntegrationTest");
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        using var activity = new Activity("test.order").SetIdFormat(ActivityIdFormat.W3C).Start();
        logger.Information("{Component}.{Method} Quantity={Quantity} OrderPrice={OrderPrice} OperationId={OperationId}",
            "BrokerOrder", "PlaceAsync", 10, -15.85m, Guid.Parse("a1000000-0000-0000-0000-000000000001"));
        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var resourceLogs = Fields(payload).Single(field => field.Number == 1);
        var resource = Fields(resourceLogs.Bytes).Single(field => field.Number == 1);
        Assert.Contains("IFM.Logging.IntegrationTest", Encoding.UTF8.GetString(resource.Bytes));
        var scopeLogs = Fields(resourceLogs.Bytes).Single(field => field.Number == 2);
        var record = Fields(scopeLogs.Bytes).Single(field => field.Number == 2);
        var fields = Fields(record.Bytes).ToArray();
        Assert.Equal(9UL, fields.Single(field => field.Number == 2).Value);
        var attributes = fields.Where(field => field.Number == 6).ToDictionary(field =>
            Encoding.UTF8.GetString(Fields(field.Bytes).Single(item => item.Number == 1).Bytes), field =>
                Fields(Fields(field.Bytes).Single(item => item.Number == 2).Bytes).Single());
        Assert.Equal("PlaceAsync", Encoding.UTF8.GetString(attributes["Method"].Bytes));
        Assert.Equal(10UL, attributes["Quantity"].Value);
        Assert.Equal(-15.85, BitConverter.ToDouble(attributes["OrderPrice"].Bytes));
        Assert.Equal("a1000000-0000-0000-0000-000000000001", Encoding.UTF8.GetString(attributes["OperationId"].Bytes));
        Assert.Equal(Convert.FromHexString(activity.TraceId.ToString()), fields.Single(field => field.Number == 9).Bytes);
        Assert.Equal(Convert.FromHexString(activity.SpanId.ToString()), fields.Single(field => field.Number == 10).Bytes);
    }

    [Theory]
    [InlineData("relative", "http/protobuf")]
    [InlineData("http://127.0.0.1:4318/v1/logs", "invalid")]
    public void Invalid_export_configuration_is_rejected_before_startup(string endpoint, string protocol)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Telemetry:Logs:OtlpEndpoint"] = endpoint, ["Telemetry:Logs:OtlpProtocol"] = protocol }).Build();
        Assert.Throws<InvalidOperationException>(() => new OtlpStructuredLogSink(config, "IFM"));
    }

    // A tiny wire decoder keeps the test independent of the exporter's internal generated protobuf types.
    readonly record struct Field(int Number, ulong Value, byte[] Bytes);
    static IEnumerable<Field> Fields(byte[] bytes)
    {
        var fields = new List<Field>();
        var offset = 0;
        while (offset < bytes.Length)
        {
            var tag = Varint(bytes, ref offset);
            var number = (int)(tag >> 3);
            var wire = tag & 7;
            if (wire == 0) fields.Add(new(number, Varint(bytes, ref offset), []));
            else
            {
                var length = wire switch { 1 => 8, 2 => checked((int)Varint(bytes, ref offset)), 5 => 4, _ => throw new InvalidDataException() };
                fields.Add(new(number, 0, bytes.AsSpan(offset, length).ToArray()));
                offset += length;
            }
        }
        return fields;
    }
    static ulong Varint(byte[] bytes, ref int offset)
    {
        ulong value = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            var next = bytes[offset++]; value |= (ulong)(next & 127) << shift;
            if ((next & 128) == 0) return value;
        }
        throw new InvalidDataException();
    }
}
