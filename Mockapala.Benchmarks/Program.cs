using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Mockapala.Benchmarks;

// Run all benchmarks in the assembly.
// Usage:
//   dotnet run -c Release                        → runs all benchmarks
//   dotnet run -c Release -- --filter "*Schema*"  → runs only schema benchmarks
//   dotnet run -c Release -- --filter "*Data*"    → runs only data generation benchmarks
BenchmarkSwitcher
    .FromAssembly(typeof(SchemaBuildBenchmarks).Assembly)
    .Run(args, DefaultConfig.Instance);
