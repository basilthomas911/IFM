using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;
/// <summary>Duplicate startup commands may resume only the same audited operation.</summary>
public static class ParameterStartupOperationModel
{
 public static void ValidateDuplicate(IParameterStartupMutation command,string storedName,string storedStream,string json)
 {
  using var document=JsonDocument.Parse(json);var value=document.RootElement;
  if(storedName!=command.CommandName||storedStream!=command.StreamId||
   value.GetProperty("CommandId").GetGuid()!=command.CommandId||value.GetProperty("RunId").GetGuid()!=command.RunId||
   value.GetProperty("ExpectedFingerprint").GetString()!=command.ExpectedFingerprint||
   value.GetProperty("EntityId").GetProperty("Id").GetGuid()!=command.EntityId.Id||
   value.GetProperty("PostEvents").GetBoolean()!=command.PostEvents)
   throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
  if(command is RecordSignalStartupReportCommand report &&
   !System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(value.GetProperty("Report").GetRawText()),JsonSerializer.SerializeToNode(report.Report)))
   throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
 }
}
