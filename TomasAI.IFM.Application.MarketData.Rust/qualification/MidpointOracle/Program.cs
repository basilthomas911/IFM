using System.Text.Json;
var cases=new List<object>();
void Add(long bid,long ask){double? midpoint=bid>0 && ask>=bid?(double?)((bid/1000000000m)/2m+(ask/1000000000m)/2m):null;cases.Add(new {bid,ask,bits=midpoint is {} value?(ulong?)BitConverter.DoubleToUInt64Bits(value):null});}
foreach(var bid in new[]{-1L,0,1,2,3,9,10,99,100,999,1000000000,6500000000000,long.MaxValue-100,long.MaxValue})foreach(var spread in new[]{0L,1,2,3,7,99})if(bid<=long.MaxValue-spread)Add(bid,bid+spread);
var random=new Random(777);for(var i=0;i<10000;i++){long bid=random.NextInt64(1,long.MaxValue-100000);Add(bid,bid+random.NextInt64(100000));}
File.WriteAllText(args[0],JsonSerializer.Serialize(cases));Console.WriteLine($"C# decimal midpoint: {cases.Count} exact conversion cases");
