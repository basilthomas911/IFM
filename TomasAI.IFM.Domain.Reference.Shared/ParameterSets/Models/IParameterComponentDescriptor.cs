using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

public interface IParameterComponentDescriptor
{
    ParameterComponentSummary Summary { get; }
    string CreateDraftPayload(Guid setId);
    ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion);
}
