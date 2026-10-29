using System.Collections.ObjectModel;
using LabManager.Core.Models;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class ProcessListViewModel : ViewModelBase
{
    private readonly ClassroomManager _manager;
    private readonly string _studentIp;
    private ProcessItem? _selectedProcess;

    public string Title { get; }
    public ObservableCollection<ProcessItem> Processes { get; } = new();

    public ProcessItem? SelectedProcess
    {
        get => _selectedProcess;
        set => SetProperty(ref _selectedProcess, value);
    }

    public RelayCommand KillSelectedCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public ProcessListViewModel(string machineName, string studentIp, ClassroomManager manager)
    {
        _studentIp = studentIp;
        _manager = manager;
        Title = $"Danh sách ứng dụng - {machineName} ({studentIp})";

        KillSelectedCommand = new RelayCommand(async () => await KillSelectedAsync());
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
    }

    public void UpdateProcesses(IEnumerable<ProcessItem> list)
    {
        Processes.Clear();
        foreach (var item in list)
        {
            Processes.Add(item);
        }
    }

    private async Task RefreshAsync()
    {
        await _manager.RequestProcessListAsync(_studentIp);
    }

    private async Task KillSelectedAsync()
    {
        if (SelectedProcess == null) return;
        await _manager.SendKillProcessAsync(_studentIp, SelectedProcess.ProcessName, SelectedProcess.Pid);
        Processes.Remove(SelectedProcess);
    }
}
