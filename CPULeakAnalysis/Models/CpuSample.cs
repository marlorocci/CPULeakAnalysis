namespace CPULeakAnalysis.Models;

public sealed record CpuSample(
    DateTime TimestampUtc,
    int ProcessId,
    string ProcessName,
    double CpuPercent,
    long WorkingSetBytes,
    long PrivateBytes,
    int HandleCount,
    int ThreadCount)
{
    public string DisplayName => $"{ProcessName} ({ProcessId})";

    public DateTime LocalTimestamp => TimestampUtc.ToLocalTime();

    public double WorkingSetMb => WorkingSetBytes / 1024d / 1024d;

    public double PrivateBytesMb => PrivateBytes / 1024d / 1024d;
}