using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class WebFilterConfigViewModel : ViewModelBase
{
    private readonly ClassroomManager _manager;
    private string _domainBlacklistText = 
        "facebook.com, tiktok.com, youtube.com, discord.com, roblox.com, instagram.com, twitter.com, x.com, netflix.com, twitch.tv";

    public string DomainBlacklistText
    {
        get => _domainBlacklistText;
        set => SetProperty(ref _domainBlacklistText, value);
    }

    public RelayCommand ApplyCommand { get; }
    public RelayCommand ClearCommand { get; }
    public event Action? OnRequestClose;

    public WebFilterConfigViewModel(ClassroomManager manager)
    {
        _manager = manager;

        ApplyCommand = new RelayCommand(async () =>
        {
            var domains = DomainBlacklistText.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(s => s.Trim())
                                             .Where(s => !string.IsNullOrWhiteSpace(s));

            await _manager.SendSetWebBlacklistAllAsync(domains);
            OnRequestClose?.Invoke();
        });

        ClearCommand = new RelayCommand(async () =>
        {
            await _manager.SendClearWebBlacklistAllAsync();
            OnRequestClose?.Invoke();
        });
    }
}
