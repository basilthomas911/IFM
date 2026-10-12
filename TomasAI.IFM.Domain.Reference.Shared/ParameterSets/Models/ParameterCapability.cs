namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Capabilities enforced by Parameter Sets handlers.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is an existing meaningful contract value; introducing an alias would change validation and serialization names.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
public enum ParameterCapability : byte { Read = 1, Author = 2, Publish = 3, Assign = 4, Retire = 5 }
