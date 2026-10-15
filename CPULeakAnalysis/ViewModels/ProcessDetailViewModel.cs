using CommunityToolkit.Mvvm.ComponentModel;
using CPULeakAnalysis.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace CPULeakAnalysis.ViewModels;

public partial class ProcessDetailViewModel : ObservableObject
{
    private readonly ObservableCollection<CpuSample> _sourceSamples;
    public ObservableCollection<string> TimeRanges { get; } =
[
    "1 Hour",
    "6 Hours",
    "12 Hours",
    "24 Hours",
    "3 Days",
    "7 Days"
];
    public TimeSpan SelectedTimeSpan =>
    SelectedTimeRange switch
    {
        "1 Hour" => TimeSpan.FromHours(1),
        "6 Hours" => TimeSpan.FromHours(6),
        "12 Hours" => TimeSpan.FromHours(12),
        "24 Hours" => TimeSpan.FromHours(24),
        "3 Days" => TimeSpan.FromDays(3),
        "7 Days" => TimeSpan.FromDays(7),
        _ => TimeSpan.FromHours(1)
    };
    [ObservableProperty]
    private string selectedTimeRange = "7 Days";

    partial void OnSelectedTimeRangeChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedTimeSpan));
    }


    public ICollectionView FilteredSamples { get; }
    public int ProcessId { get; }

    public string ProcessName { get; }

    public ICollectionView ProcessSamples { get; }

    public ProcessDetailViewModel(
        ObservableCollection<CpuSample> sourceSamples,
        int processId,
        string processName)
    {
        _sourceSamples = sourceSamples;

        ProcessId = processId;
        ProcessName = processName;

        FilteredSamples =
            new ListCollectionView(_sourceSamples);

        FilteredSamples.Filter = item =>
        {
            return item is CpuSample sample &&
                   sample.ProcessId == ProcessId;
        };
    }
}