namespace CPULeakAnalysis.Models;
internal sealed record ProcessCpuState(TimeSpan TotalProcessorTime, DateTime ObservedUtc);