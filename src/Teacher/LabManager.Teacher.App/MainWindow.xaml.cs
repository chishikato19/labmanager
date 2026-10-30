using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LabManager.Teacher.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _isMouseDown;
    private bool _isDragging;
    private Point _startPoint;
    private readonly Dictionary<ViewModels.StudentCardViewModel, bool> _initialSelection = new();

    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }

    private void StudentCard_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ViewModels.StudentCardViewModel card)
        {
            (DataContext as ViewModels.DashboardViewModel)?.OnStudentHoverChanged(card, isHovered: true);
        }
    }

    private void StudentCard_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ViewModels.StudentCardViewModel card)
        {
            (DataContext as ViewModels.DashboardViewModel)?.OnStudentHoverChanged(card, isHovered: false);
        }
    }

    private void DropdownGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void StudentScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (DataContext is ViewModels.DashboardViewModel vm)
            {
                vm.CardWidth += (e.Delta > 0 ? 15 : -15);
                e.Handled = true;
            }
        }
    }

    private void StudentScrollViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ViewModels.DashboardViewModel vm) return;

        // Ignore clicks originating from active collection banner or action buttons
        if (IsInsideElement(e.OriginalSource as DependencyObject, "CollectionBannerBorder"))
            return;

        _isMouseDown = true;
        _isDragging = false;
        _startPoint = e.GetPosition(SelectionCanvas);

        _initialSelection.Clear();
        foreach (var s in vm.DisplayedStudents.OfType<ViewModels.StudentCardViewModel>())
        {
            _initialSelection[s] = s.IsSelected;
        }
    }

    private void StudentScrollViewer_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDown) return;

        Point currentPoint = e.GetPosition(SelectionCanvas);

        if (!_isDragging)
        {
            // Check minimum drag distance before initiating marquee box
            if (Math.Abs(currentPoint.X - _startPoint.X) > 4 || Math.Abs(currentPoint.Y - _startPoint.Y) > 4)
            {
                _isDragging = true;
                StudentScrollViewer.CaptureMouse();
                SelectionRectangle.Visibility = Visibility.Visible;
            }
        }

        if (_isDragging)
        {
            double left = Math.Min(_startPoint.X, currentPoint.X);
            double top = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - _startPoint.X);
            double height = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionRectangle, left);
            Canvas.SetTop(SelectionRectangle, top);
            SelectionRectangle.Width = width;
            SelectionRectangle.Height = height;

            Rect selectionRect = new Rect(left, top, width, height);
            bool isCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            UpdateSelection(selectionRect, isCtrl);

            // Auto-scroll when dragging near viewport boundary
            Point scrollPos = e.GetPosition(StudentScrollViewer);
            if (scrollPos.Y < 30)
            {
                StudentScrollViewer.ScrollToVerticalOffset(StudentScrollViewer.VerticalOffset - 16);
            }
            else if (scrollPos.Y > StudentScrollViewer.ActualHeight - 30)
            {
                StudentScrollViewer.ScrollToVerticalOffset(StudentScrollViewer.VerticalOffset + 16);
            }
        }
    }

    private void StudentScrollViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMouseDown) return;

        if (_isDragging)
        {
            StudentScrollViewer.ReleaseMouseCapture();
            _isDragging = false;
            SelectionRectangle.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Click without dragging
            SelectionRectangle.Visibility = Visibility.Collapsed;

            // If clicked on empty space (not on any student card) and not holding Ctrl, clear selection
            var card = FindCardFromVisual(e.OriginalSource as DependencyObject);
            if (card == null && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (DataContext is ViewModels.DashboardViewModel vm)
                {
                    vm.DeselectAll();
                }
            }
        }

        _isMouseDown = false;
    }

    private void StudentScrollViewer_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            _isMouseDown = false;
        }
    }

    private void StudentScrollViewer_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _isMouseDown = false;
        _isDragging = false;
        if (SelectionRectangle != null)
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateSelection(Rect selectionRect, bool isCtrl)
    {
        if (DataContext is not ViewModels.DashboardViewModel vm) return;

        foreach (var student in vm.DisplayedStudents)
        {
            if (student is not ViewModels.StudentCardViewModel card) continue;

            var container = StudentItemsControl.ItemContainerGenerator.ContainerFromItem(card) as FrameworkElement;
            if (container == null || !container.IsVisible) continue;

            try
            {
                GeneralTransform transform = container.TransformToVisual(SelectionCanvas);
                Rect bounds = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));

                bool intersects = selectionRect.IntersectsWith(bounds);

                if (isCtrl)
                {
                    bool wasSelected = _initialSelection.TryGetValue(card, out bool sel) && sel;
                    card.IsSelected = wasSelected || intersects;
                }
                else
                {
                    card.IsSelected = intersects;
                }
            }
            catch (InvalidOperationException)
            {
                // Visual tree element might be detached
            }
        }
    }

    private static ViewModels.StudentCardViewModel? FindCardFromVisual(DependencyObject? visual)
    {
        while (visual != null)
        {
            if (visual is FrameworkElement fe && fe.DataContext is ViewModels.StudentCardViewModel card)
            {
                return card;
            }
            visual = VisualTreeHelper.GetParent(visual);
        }
        return null;
    }

    private static bool IsInsideElement(DependencyObject? visual, string elementName)
    {
        while (visual != null)
        {
            if (visual is FrameworkElement fe && fe.Name == elementName)
            {
                return true;
            }
            visual = VisualTreeHelper.GetParent(visual);
        }
        return false;
    }
}
