using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;

// Every benchmark reports allocations: the render loop budget is 0 B (AGENTS.md 2.3).
var config = DefaultConfig.Instance.AddDiagnoser(MemoryDiagnoser.Default);
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
