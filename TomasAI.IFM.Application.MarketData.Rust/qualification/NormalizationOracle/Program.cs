using System.Reflection;
using System.Text.Json;

if (args.Length != 1) throw new ArgumentException("Expected output fixture path.");
// Reflection accesses the unchanged production internal functions. It is confined
// to this oracle; no reflection or copied expected formula is used in production.
var type = Assembly.Load("TomasAI.IFM.Framework.MarketData.DataBento").GetType("TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.DatabentoTradeNormalizer", throwOnError: true)!;
MethodInfo Method(string name) => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingMethodException(type.FullName, name);
var action = Method("MapAction");
var side = Method("MapSide");
var conditions = Method("MapConditions");
var flags = new ushort[65_536];
for (var header = 0; header < 256; header++)
    for (var dbn = 0; dbn < 256; dbn++)
        flags[(header * 256) + dbn] = Convert.ToUInt16(conditions.Invoke(null, [(byte)header, (byte)dbn]));
var result = new
{
    Schema = 1,
    Baseline = "Unchanged production DatabentoTradeNormalizer",
    Actions = Enumerable.Range(0, 256).Select(input => Convert.ToInt32(action.Invoke(null, [(byte)input]))).ToArray(),
    Sides = Enumerable.Range(0, 256).Select(input => Convert.ToInt32(side.Invoke(null, [(byte)input]))).ToArray(),
    ConditionsHeaderMajor = flags
};
File.WriteAllText(args[0], JsonSerializer.Serialize(result) + Environment.NewLine);
Console.WriteLine("Production C# oracle: 256 action bytes; 256 side bytes; all 65536 header/DBN flag combinations.");
