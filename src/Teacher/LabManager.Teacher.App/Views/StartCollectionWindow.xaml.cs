using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace LabManager.Teacher.App.Views;

public partial class StartCollectionWindow : Window
{
    public string SelectedFolder { get; private set; } = string.Empty;
    public string SessionTitle { get; private set; } = "Nộp bài cho Giáo viên";
    public bool PrefixMachineName => ChkPrefixMachineName.IsChecked == true;
    public bool SeparateMachineFolder => ChkSeparateMachineFolder.IsChecked == true;

    public StartCollectionWindow(string defaultFolder = "")
    {
        InitializeComponent();

        if (string.IsNullOrWhiteSpace(defaultFolder))
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folderName = $"ThuBai_{DateTime.Now:yyyyMMdd_HHmm}";
            defaultFolder = Path.Combine(docs, "LabManager", folderName);
        }

        TxtFolderPath.Text = defaultFolder;
        SelectedFolder = defaultFolder;
        UpdatePreview();
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu bài thu từ học sinh",
            Multiselect = false
        };

        if (Directory.Exists(SelectedFolder))
        {
            dialog.InitialDirectory = SelectedFolder;
        }

        if (dialog.ShowDialog(this) == true)
        {
            SelectedFolder = dialog.FolderName;
            TxtFolderPath.Text = SelectedFolder;
            UpdatePreview();
        }
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        string baseDir = string.IsNullOrWhiteSpace(SelectedFolder) ? "ThưMục" : Path.GetFileName(SelectedFolder);
        if (string.IsNullOrEmpty(baseDir)) baseDir = "ThưMục";

        string preview;
        bool prefix = ChkPrefixMachineName.IsChecked == true;
        bool separate = ChkSeparateMachineFolder.IsChecked == true;

        if (separate && prefix)
        {
            preview = $@"{baseDir}\PC01\PC01_bai1.cpp";
        }
        else if (separate)
        {
            preview = $@"{baseDir}\PC01\bai1.cpp";
        }
        else if (prefix)
        {
            preview = $@"{baseDir}\PC01_bai1.cpp";
        }
        else
        {
            preview = $@"{baseDir}\bai1.cpp";
        }

        TxtPathPreview.Text = preview;
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtFolderPath.Text))
        {
            MessageBox.Show("Vui lòng chọn thư mục lưu bài trước khi bắt đầu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedFolder = TxtFolderPath.Text.Trim();
        SessionTitle = string.IsNullOrWhiteSpace(TxtSessionTitle.Text) ? "Nộp bài cho Giáo viên" : TxtSessionTitle.Text.Trim();

        try
        {
            if (!Directory.Exists(SelectedFolder))
            {
                Directory.CreateDirectory(SelectedFolder);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể tạo thư mục lưu bài: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
