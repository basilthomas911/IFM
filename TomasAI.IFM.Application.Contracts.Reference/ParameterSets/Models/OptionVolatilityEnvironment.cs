namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
public enum OptionVolatilityEnvironment : byte { Unknown = 0, Production = 1, Paper = 2, Simulation = 3 }
