using BenchmarkDotNet.Running;

using DeltaBuild.Benchmarks;

BenchmarkRunner.Run<DiffCalculatorBenchmarks>(args: args);