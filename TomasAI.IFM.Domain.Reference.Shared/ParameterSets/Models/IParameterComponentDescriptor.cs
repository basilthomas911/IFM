using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

public interface IParameterComponentDescriptor
{
    ParameterComponentSummary Summary { get; }
    ParameterSetValue CreateTypedDraft(Guid setId);
    ParameterValidationIssue[] ValidateTyped(ParameterSetValue parameters, int schemaVersion);
    string CreateDraftPayload(Guid setId);
    ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion);
}
