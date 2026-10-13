using System.Diagnostics;
using CPULeakAnalysis.Models;
namespace CPULeakAnalysis.Services;
public sealed class MilestoneProcessSampler
{
    private readonly Dictionary<int, ProcessCpuState> _previous = new();
    private readonly Dictionary<int, bool> _identityCache = new();
    public IReadOnlyList<CpuSample> Sample()
    {
        var now = DateTime.UtcNow; var output = new List<CpuSample>(); var live = new HashSet<int>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (!IsMilestone(p)) continue; live.Add(p.Id);
                    var total = p.TotalProcessorTime;
                    if (_previous.TryGetValue(p.Id, out var prior))
                    {
                        var wallMs = (now - prior.ObservedUtc).TotalMilliseconds;
                        var cpuMs = (total - prior.TotalProcessorTime).TotalMilliseconds;
                        var percent = wallMs <= 0 ? 0 : Math.Clamp(cpuMs / wallMs / Environment.ProcessorCount * 100d, 0d, 100d);
                        output.Add(new CpuSample(
    now,
    p.Id,
    p.ProcessName,
    percent,
    p.WorkingSet64,
    p.PrivateMemorySize64,
    p.HandleCount,
    p.Threads.Count));
                    }
                    _previous[p.Id] = new(total, now);
                }
                catch { _previous.Remove(p.Id); _identityCache.Remove(p.Id); }
            }
        }
        foreach (var pid in _previous.Keys.Where(x => !live.Contains(x)).ToList()) { _previous.Remove(pid); _identityCache.Remove(pid); }
        return output;
    }
    private bool IsMilestone(Process p)
    {
        if (_identityCache.TryGetValue(p.Id, out var known)) return known;
        bool Match(string? s) => !string.IsNullOrWhiteSpace(s) && (s.Contains("Milestone", StringComparison.OrdinalIgnoreCase) || s.Contains("VideoOS", StringComparison.OrdinalIgnoreCase) || s.Contains("XProtect", StringComparison.OrdinalIgnoreCase));
        var match = Match(p.ProcessName);
        try { var m=p.MainModule; match |= Match(m?.FileName) || Match(m?.FileVersionInfo.CompanyName) || Match(m?.FileVersionInfo.ProductName) || Match(m?.FileVersionInfo.FileDescription); } catch { }
        _identityCache[p.Id] = match; return match;
    }
}