using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using LabManager.Protocol.Packets;
using LabManager.Student.Core.Services;

namespace LabManager.Student.App.Views;

public partial class StudentSubmissionDialogWindow : Window
{
    private readonly StudentCoordinator _coordinator;
    private readonly FileCollectRequestPacket? _session;
    private readonly List<string> _selectedFiles = new();
    private bool _canClose;

    public StudentSubmissionDialogWindow(StudentCoordinator coordinator, FileCollectRequestPacket? session)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _session = session;

        TxtMachineName.Text = Environment.MachineName;
        TxtStudentName.Text = Environment.UserName;

        if (session != null && !string.IsNullOrWhiteSpace(session.SessionTitle))
        {
            TxtSessionTitle.Text = session.SessionTitle;
        }

        Closing += (_, e) =>
        {
            if (!_canClose)
            {
                e.Cancel = true;
                Hide();
            }
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn tệp bài làm để nộp cho giáo viên",
            Multiselect = true,
            Filter = "Tất cả tệp (*.*)|*.*|Tài liệu (*.docx;*.doc;*.pdf;*.pptx;*.xlsx)|*.docx;*.doc;*.pdf;*.pptx;*.xlsx|Mã nguồn (*.py;*.cpp;*.c;*.pas;*.java;*.cs;*.html;*.css;*.js)|*.py;*.cpp;*.c;*.pas;*.java;*.cs;*.html;*.css;*.js|Tệp nén (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z"
        };

        if (dialog.ShowDialog() == true)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddFiles(files);
        }
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (!_selectedFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    _selectedFiles.Add(path);
                }
            }
            else if (Directory.Exists(path))
            {
                // Nếu kéo cả thư mục, lấy toàn bộ file trong thư mục
                var subFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (var sf in subFiles)
                {
                    if (!_selectedFiles.Contains(sf, StringComparer.OrdinalIgnoreCase))
                    {
                        _selectedFiles.Add(sf);
                    }
                }
            }
        }

        RefreshFilesUI();
    }

    private void BtnClearFiles_Click(object sender, RoutedEventArgs e)
    {
        _selectedFiles.Clear();
        RefreshFilesUI();
    }

    private void RefreshFilesUI()
    {
        FilesPanel.Children.Clear();

        if (_selectedFiles.Count == 0)
        {
            FilesPanel.Children.Add(TxtNoFiles);
            BtnSubmit.IsEnabled = false;
            SetStatus("Chưa chọn tệp bài làm.", StatusType.Normal);
            return;
        }

        long totalBytes = 0;
        foreach (var file in _selectedFiles)
        {
            long size = 0;
            try
            {
                var fi = new FileInfo(file);
                size = fi.Length;
                totalBytes += size;
            }
            catch { }

            var row = new Grid { Margin = new Thickness(4, 3, 4, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            infoPanel.Children.Add(new TextBlock
            {
                Text = "📄 " + Path.GetFileName(file),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 340
            });
            infoPanel.Children.Add(new TextBlock
            {
                Text = $" ({FormatFileSize(size)})",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 11,
                Margin = new Thickness(6, 0, 0, 0)
            });

            var btnRemove = new Button
            {
                Content = "✕",
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(6, 2, 6, 2),
                Tag = file
            };
            btnRemove.Click += (s, _) =>
            {
                if (s is Button b && b.Tag is string f)
                {
                    _selectedFiles.Remove(f);
                    RefreshFilesUI();
                }
            };

            Grid.SetColumn(infoPanel, 0);
            Grid.SetColumn(btnRemove, 1);
            row.Children.Add(infoPanel);
            row.Children.Add(btnRemove);

            FilesPanel.Children.Add(row);
        }

        BtnSubmit.IsEnabled = true;
        SetStatus($"Đã chọn {_selectedFiles.Count} tệp ({FormatFileSize(totalBytes)}). Sẵn sàng gửi bài.", StatusType.Ready);
    }

    private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFiles.Count == 0) return;

        string studentName = TxtStudentName.Text.Trim();
        if (string.IsNullOrWhiteSpace(studentName))
        {
            studentName = Environment.UserName;
        }

        string note = TxtNote.Text.Trim();

        BtnSubmit.IsEnabled = false;
        SetStatus("⏳ Đang chuẩn bị gửi tệp bài làm...", StatusType.InProgress);

        var progress = new Progress<(int current, int total, string fileName)>(info =>
        {
            SetStatus($"⏳ Đang gửi ({info.current}/{info.total}): {info.fileName}...", StatusType.InProgress);
        });

        try
        {
            bool success = await _coordinator.SubmitFilesToTeacherAsync(studentName, note, _selectedFiles, progress);
            if (success)
            {
                SetStatus($"✅ ĐÃ NỘP THÀNH CÔNG {_selectedFiles.Count} TỆP lúc {DateTime.Now:HH:mm:ss}!", StatusType.Success);
                BtnSubmit.Content = "✅ GỬI LẠI / CẬP NHẬT";
                BtnSubmit.IsEnabled = true;
            }
            else
            {
                SetStatus("❌ Nộp bài thất bại! Vui lòng kiểm tra kết nối với giáo viên và thử lại.", StatusType.Error);
                BtnSubmit.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Lỗi: {ex.Message}", StatusType.Error);
            BtnSubmit.IsEnabled = true;
        }
    }

    private void SetStatus(string message, StatusType type)
    {
        TxtStatus.Text = message;
        switch (type)
        {
            case StatusType.Success:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                break;
            case StatusType.Error:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                break;
            case StatusType.InProgress:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253));
                break;
            case StatusType.Ready:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(167, 243, 208));
                break;
            default:
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                break;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    public void ForceClose()
    {
        _canClose = true;
        Close();
    }

    private enum StatusType
    {
        Normal,
        Ready,
        InProgress,
        Success,
        Error
    }
}
