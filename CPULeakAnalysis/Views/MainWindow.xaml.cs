using CPULeakAnalysis.Models;
using CPULeakAnalysis.Services;
using CPULeakAnalysis.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace CPULeakAnalysis.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ProcessDataGrid_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (ProcessDataGrid.SelectedItem is not CpuSample selectedSample)
        {
            return;
        }

        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        var detailViewModel = new ProcessDetailViewModel(
            mainViewModel.Samples,
            selectedSample.ProcessId,
            selectedSample.ProcessName);

        var detailWindow = new ProcessDetailWindow
        {
            Owner = this,
            DataContext = detailViewModel
        };

        detailWindow.Show();
    }
}