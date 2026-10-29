using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LabManager.Student.App.Views;

public partial class ScreenBroadcastWindow : Window
{
    public ScreenBroadcastWindow()
    {
        InitializeComponent();
    }

    public void UpdateFrame(byte[] jpegBytes)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                using var ms = new MemoryStream(jpegBytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();

                ImgScreen.Source = bmp;
            }
            catch
            {
                // Bỏ qua nếu khung hình bị lỗi mạng
            }
        });
    }

    public void ShowBroadcast(bool fullscreen = true, string title = "Đang theo dõi màn hình bài giảng của Giáo viên")
    {
        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                TxtBroadcastTitle.Text = title;
            }

            if (fullscreen)
            {
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
                Topmost = true;
            }
            else
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
                Topmost = false;
            }

            Show();
            Activate();
        });
    }

    public void HideBroadcast()
    {
        Dispatcher.Invoke(() =>
        {
            Hide();
            ImgScreen.Source = null;
        });
    }
}
