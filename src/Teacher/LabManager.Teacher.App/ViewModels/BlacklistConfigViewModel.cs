using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class BlacklistConfigViewModel : ViewModelBase
{
    private readonly ClassroomManager _manager;
    private string _blacklistText = "cmd, powershell, cheatengine, discord, roblox, steam, taskmgr";

    public string BlacklistText
    {
        get => _blacklistText;
        set => SetProperty(ref _blacklistText, value);
    }

    public RelayCommand ApplyCommand { get; }
    public RelayCommand ClearCommand { get; }
    public event Action? OnRequestClose;

    public BlacklistConfigViewModel(ClassroomManager manager)
    {
        _manager = manager;

        ApplyCommand = new RelayCommand(async () =>
        {
            var items = BlacklistText.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim())
                                     .Where(s => !string.IsNullOrWhiteSpace(s));

            await _manager.SendSetBlacklistAllAsync(items);
            OnRequestClose?.Invoke();
        });

        ClearCommand = new RelayCommand(async () =>
        {
            await _manager.SendClearBlacklistAllAsync();
            OnRequestClose?.Invoke();
        });
    }
}
