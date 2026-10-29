using System.Windows;
using System.Windows.Input;
using LabManager.Protocol.Packets;
using LabManager.Student.Core.Services;

namespace LabManager.Student.App.Views;

public partial class StudentSubmissionBarWindow : Window
{
    private readonly StudentCoordinator _coordinator;
    private FileCollectRequestPacket? _session;
    private StudentSubmissionDialogWindow? _dialog;
    private bool _canClose;

    public StudentSubmissionBarWindow(StudentCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;

        Closing += (_, e) =>
        {
            if (!_canClose)
            {
                e.Cancel = true;
                Hide();
            }
        };
    }

    public void ShowBar(FileCollectRequestPacket session)
    {
        _session = session;
        if (!string.IsNullOrWhiteSpace(session.SessionTitle))
        {
            TxtTitle.Text = session.SessionTitle.ToUpperInvariant();
        }

        // Định vị chính giữa phía trên cùng màn hình
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + 8;

        Show();
        Topmost = true;
    }

    public void HideBar()
    {
        Hide();
        _dialog?.Close();
    }

    private void Pill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_dialog == null || !_dialog.IsLoaded)
        {
            _dialog = new StudentSubmissionDialogWindow(_coordinator, _session);
        }

        _dialog.Show();
        _dialog.Activate();
        _dialog.Topmost = true;
    }

    public void ForceClose()
    {
        _canClose = true;
        _dialog?.ForceClose();
        Close();
    }
}
