using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPULeakAnalysis.Models;
using CPULeakAnalysis.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;


namespace CPULeakAnalysis.ViewModels;
public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _db = new(); private readonly MilestoneProcessSampler _sampler = new(); private readonly DispatcherTimer _timer;
    public ObservableCollection<string> TimeRanges { get; } =
[
    "1 Hour",
    "6 Hours",
    "12 Hours",
    "24 Hours",
    "3 Days",
    "7 Days"
];
    public ObservableCollection<int> ProcessIds { get; } = new();

    public ICollectionView FilteredSamples { get; }

    [ObservableProperty]
    private int? selectedProcessId;

    [ObservableProperty]
    private int? activeProcessId;

    public string FilterStatus =>
        ActiveProcessId == 0
            ? "Displaying all process IDs"
            : $"Displaying PID {ActiveProcessId}";
    [ObservableProperty]
    private string selectedTimeRange = "24 Hours";
    public TimeSpan SelectedTimeSpan =>
    SelectedTimeRange switch
    {
        "1 Hour" => TimeSpan.FromHours(1),
        "6 Hours" => TimeSpan.FromHours(6),
        "12 Hours" => TimeSpan.FromHours(12),
        "24 Hours" => TimeSpan.FromHours(24),
        "3 Days" => TimeSpan.FromDays(3),
        "7 Days" => TimeSpan.FromDays(7),
        _ => TimeSpan.FromHours(24)
    };

    partial void OnSelectedTimeRangeChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedTimeSpan));
    }

    [ObservableProperty]
    private int zoomPercent = 25;
    public ObservableCollection<CpuSample> Samples { get; } = new();
    [ObservableProperty] private bool isMonitoring;
    [ObservableProperty] private string status = "Initializing...";
    [ObservableProperty] private int sampleIntervalSeconds = 5;
    [ObservableProperty] private double totalCpuPercent;
    [ObservableProperty] private string databasePath = "";
    public string MonitorButtonText => IsMonitoring ? "Stop monitoring" : "Start monitoring";
    public MainViewModel()
    {
        FilteredSamples = CollectionViewSource.GetDefaultView(Samples);
        FilteredSamples.Filter = FilterSample;

        _timer = new DispatcherTimer();

        _timer.Tick += async (_, _) =>
            await CaptureAsync();

        PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(IsMonitoring))
            {
                OnPropertyChanged(nameof(MonitorButtonText));
            }
        };

        _ = InitializeAsync();
    }
    private bool FilterSample(object item)
    {
        if (item is not CpuSample sample)
            return false;

        if (ActiveProcessId == null)
            return true;

        return sample.ProcessId == ActiveProcessId.Value;
    }
    private void RegisterProcessId(int processId)
    {
        if (ProcessIds.Contains(processId))
            return;

        ProcessIds.Add(processId);

        var sortedProcessIds = ProcessIds
            .OrderBy(pid => pid)
            .ToList();

        ProcessIds.Clear();

        foreach (int pid in sortedProcessIds)
        {
            ProcessIds.Add(pid);
        }
    }
    [RelayCommand]
    private void ApplyProcessFilter()
    {
        ActiveProcessId = SelectedProcessId;

        FilteredSamples.Refresh();
        OnPropertyChanged(nameof(FilteredSamples));
        //OnPropertyChanged(nameof(FilterStatus));

        Status = ActiveProcessId != 0
            ? $"Display filtered to PID {ActiveProcessId}. Data collection continues for all Milestone processes."
            : "Displaying all Milestone process IDs.";
    }
    private async Task InitializeAsync()
    {
        try { await _db.InitializeAsync(); DatabasePath = _db.DatabasePath; foreach (var s in await _db.LoadRecentAsync(24)) Samples.Add(s); Status="Ready. Showing the last 24 hours."; }
        catch(Exception ex) { Status=$"Database error: {ex.Message}"; }
    }
    [RelayCommand]
    private async Task ToggleMonitoringAsync()
    {
        if (IsMonitoring)
        {
            _timer.Stop();

            IsMonitoring = false;

            Status = "Monitoring stopped.";

            return;
        }

        // Show all processes when monitoring starts
        SelectedProcessId = null;
        ActiveProcessId = null;

        FilteredSamples?.Refresh();

        _timer.Interval = TimeSpan.FromSeconds(
            Math.Clamp(SampleIntervalSeconds, 1, 3600));

        IsMonitoring = true;

        Status = "Monitoring Milestone processes...";

        // Prime CPU baselines
        _sampler.Sample();

        // Wait briefly then capture a real sample
        await Task.Delay(1000);

        await CaptureAsync();

        _timer.Start();
    }
    [RelayCommand]
    private async Task CaptureAsync()
    {
        Status = $"CaptureAsync fired {DateTime.Now:T}";

        try
        {
            IReadOnlyList<CpuSample> batch =
                _sampler.Sample();

            // Every sample is stored, regardless of the display filter.
            await _db.InsertAsync(batch);

            foreach (CpuSample sample in batch)
            {
                Samples.Add(sample);
                RegisterProcessId(sample.ProcessId);
            }

            DateTime cutoff =
                DateTime.UtcNow.AddDays(-7);

            while (Samples.Count > 0 &&
                   Samples[0].TimestampUtc < cutoff)
            {
                Samples.RemoveAt(0);
            }

            FilteredSamples.Refresh();

            TotalCpuPercent = ActiveProcessId.HasValue
                ? batch
                    .Where(sample =>
                        sample.ProcessId == ActiveProcessId.Value)
                    .Sum(sample => sample.CpuPercent)
                : batch.Sum(sample => sample.CpuPercent);

            Status = batch.Count == 0
                ? "No running Milestone processes detected."
                : $"Captured {batch.Count} Milestone process samples at {DateTime.Now:T}.";
        }
        catch (Exception ex)
        {
            Status = $"Sampling error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearDatabaseAsync()
    {
        if (MessageBox.Show(
                "Delete all stored CPU samples?",
                "Clear database",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await _db.ClearAsync();

        Samples.Clear();
        ProcessIds.Clear();

        SelectedProcessId = null;
        ActiveProcessId = null;

        FilteredSamples.Refresh();

        TotalCpuPercent = 0;

        OnPropertyChanged(nameof(FilterStatus));

        Status = "Database cleared.";
    }
}