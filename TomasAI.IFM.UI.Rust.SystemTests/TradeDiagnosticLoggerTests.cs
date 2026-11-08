using Microsoft.Extensions.Logging;
using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed class TradeDiagnosticLoggerTests
{
    [Fact] public void Writes_correlated_structured_record_without_console()
    {
        var path=Path.Combine(Path.GetTempPath(),$"ifm-diagnostics-{Guid.NewGuid():N}.mpklog");
        try {
            var logger=new RustDiagnosticLogger(path);
            var submissionId=Guid.NewGuid();
            using(logger.BeginScope(new Dictionary<string,object>{["SubmissionId"]=submissionId}))
                logger.LogInformation("OrderSubmission BrokerAccepted OrderId={OrderId}","11.17.101");
            var bytes = File.ReadAllBytes(path);
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes);
            Assert.Equal(bytes.Length - sizeof(int), length);
            using var record=JsonDocument.Parse(MessagePack.MessagePackSerializer.ConvertToJson(bytes.AsMemory(sizeof(int), length), BridgeWire.Options));
            Assert.Contains("11.17.101",record.RootElement.GetProperty("message").GetString());
            Assert.Contains(submissionId.ToString(),record.RootElement.GetProperty("scopes").GetRawText());
            Assert.Contains("OrderId",record.RootElement.GetProperty("fields").GetRawText());
        } finally { File.Delete(path); }
    }
}
