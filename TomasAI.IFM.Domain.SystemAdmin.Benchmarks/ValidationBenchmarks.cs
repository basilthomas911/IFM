using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.SystemAdmin.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ValidationBenchmarks
{
    readonly Guid _commandId = Guid.NewGuid();

    [Benchmark(Baseline = true)]
    public int BeforeAlwaysAllocateList()
    {
        var errors = new List<ValidationError>();
        if (_commandId == Guid.Empty)
            errors.Add(new ValidationError("CommandId is empty"));
        return errors.Count;
    }

    [Benchmark]
    public bool AfterAllocateOnlyOnFailure()
    {
        if (_commandId != Guid.Empty)
            return true;
        _ = new List<ValidationError>(1) { new("CommandId is empty") };
        return false;
    }
}
