using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPULeakAnalysis.Models;
using CPULeakAnalysis.Services;
namespace CPULeakAnalysis.ViewModels;
public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _db = new(); private readonly MilestoneProcessSampler _sampler = new(); private readonly DispatcherTimer _timer;
    public ObservableCollection<CpuSample> Samples { get; } = new();
    [ObservableProperty] private bool isMonitoring;
    [ObservableProperty] private string status = "Initializing...";
    [ObservableProperty] private int sampleIntervalSeconds = 5;
    [ObservableProperty] private double totalCpuPercent;
    [ObservableProperty] private string databasePath = "";
    public string MonitorButtonText => IsMonitoring ? "Stop monitoring" : "Start monitoring";
    public MainViewModel()
    {
        _timer = new DispatcherTimer(); _timer.Tick += async (_, _) => await CaptureAsync();
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsMonitoring)) OnPropertyChanged(nameof(MonitorButtonText)); };
        _ = InitializeAsync();
    }
    private async Task InitializeAsync()
    {
        try { await _db.InitializeAsync(); DatabasePath = _db.DatabasePath; foreach (var s in await _db.LoadRecentAsync(24)) Samples.Add(s); Status="Ready. Showing the last 24 hours."; }
        catch(Exception ex) { Status=$"Database error: {ex.Message}"; }
    }
    [RelayCommand] private async Task ToggleMonitoringAsync()
    {
        if (IsMonitoring) { _timer.Stop(); IsMonitoring=false; Status="Monitoring stopped."; return; }
        _timer.Interval=TimeSpan.FromSeconds(Math.Clamp(SampleIntervalSeconds,1,3600)); IsMonitoring=true; Status="Monitoring Milestone processes...";
        _sampler.Sample(); _timer.Start(); await Task.CompletedTask;
    }
    [RelayCommand] private async Task CaptureAsync()
    {
        try
        {
            var batch=_sampler.Sample(); await _db.InsertAsync(batch);
            foreach(var s in batch) Samples.Add(s);
            var cutoff=DateTime.UtcNow.AddHours(-24); while(Samples.Count>0 && Samples[0].TimestampUtc<cutoff) Samples.RemoveAt(0);
            TotalCpuPercent=batch.Sum(x=>x.CpuPercent); Status=batch.Count==0 ? "No running Milestone processes detected." : $"Captured {batch.Count} Milestone process samples at {DateTime.Now:T}.";
        }
        catch(Exception ex) { Status=$"Sampling error: {ex.Message}"; }
    }
    [RelayCommand] private async Task ClearDatabaseAsync()
    {
        if (MessageBox.Show("Delete all stored CPU samples?", "Clear database", MessageBoxButton.YesNo, MessageBoxImage.Warning)!=MessageBoxResult.Yes) return;
        await _db.ClearAsync(); Samples.Clear(); TotalCpuPercent=0; Status="Database cleared.";
    }
}