using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class RemoteControlWindow : Window
{
    private readonly RemoteControlViewModel _viewModel;

    public RemoteControlWindow(RemoteControlViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
        PreviewKeyDown += OnWindowKeyDown;
        PreviewKeyUp += OnWindowKeyUp;
    }

    private (double X, double Y)? GetNormalizedCoordinates(MouseEventArgs e)
    {
        if (ScreenImage.Source is not BitmapSource bmp || ScreenImage.ActualWidth <= 0 || ScreenImage.ActualHeight <= 0)
            return null;

        var pos = e.GetPosition(ScreenImage);
        double imageAspect = (double)bmp.PixelWidth / bmp.PixelHeight;
        double viewAspect = ScreenImage.ActualWidth / ScreenImage.ActualHeight;

        double renderW, renderH, offsetX = 0, offsetY = 0;
        if (viewAspect > imageAspect)
        {
            renderH = ScreenImage.ActualHeight;
            renderW = renderH * imageAspect;
            offsetX = (ScreenImage.ActualWidth - renderW) / 2.0;
        }
        else
        {
            renderW = ScreenImage.ActualWidth;
            renderH = renderW / imageAspect;
            offsetY = (ScreenImage.ActualHeight - renderH) / 2.0;
        }

        if (renderW <= 0 || renderH <= 0) return null;

        double normX = Math.Clamp((pos.X - offsetX) / renderW, 0.0, 1.0);
        double normY = Math.Clamp((pos.Y - offsetY) / renderH, 0.0, 1.0);
        return (normX, normY);
    }

    private void OnImageMouseMove(object sender, MouseEventArgs e)
    {
        var coords = GetNormalizedCoordinates(e);
        if (coords.HasValue)
            _viewModel.SendMouseMove(coords.Value.X, coords.Value.Y);
    }

    private void OnImageMouseDown(object sender, MouseButtonEventArgs e)
    {
        ScreenImage.Focus();
        var coords = GetNormalizedCoordinates(e);
        if (!coords.HasValue) return;

        int button = e.ChangedButton == MouseButton.Right ? 1 : (e.ChangedButton == MouseButton.Middle ? 2 : 0);
        _viewModel.SendMouseButton(coords.Value.X, coords.Value.Y, button, isDown: true);
        e.Handled = true;
    }

    private void OnImageMouseUp(object sender, MouseButtonEventArgs e)
    {
        var coords = GetNormalizedCoordinates(e);
        if (!coords.HasValue) return;

        int button = e.ChangedButton == MouseButton.Right ? 1 : (e.ChangedButton == MouseButton.Middle ? 2 : 0);
        _viewModel.SendMouseButton(coords.Value.X, coords.Value.Y, button, isDown: false);
        e.Handled = true;
    }

    private void OnImageMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _viewModel.SendMouseWheel(e.Delta);
        e.Handled = true;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk > 0)
        {
            _viewModel.SendKeyEvent(vk, isDown: true);
            e.Handled = true;
        }
    }

    private void OnWindowKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk > 0)
        {
            _viewModel.SendKeyEvent(vk, isDown: false);
            e.Handled = true;
        }
    }
}
