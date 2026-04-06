using BenchmarkDotNet.Running;

using DeltaBuild.Benchmarks;

using Microsoft.Build.Locator;

MSBuildLocator.RegisterDefaults();

BenchmarkRunner.Run<SnapshotGeneratorBenchmarks>(args: args);
