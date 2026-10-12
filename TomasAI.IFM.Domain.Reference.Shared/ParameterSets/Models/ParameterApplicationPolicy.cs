using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
public enum ParameterApplicationPolicy : byte { Unknown = 0, NextOperation = 1, NextStartup = 2, ExplicitReload = 3 }
