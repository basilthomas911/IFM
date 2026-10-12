using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
public sealed record EmulatorOperationCheckpoint(Guid OperationId, string Hash, FrameworkDispatchReceipt Receipt);
