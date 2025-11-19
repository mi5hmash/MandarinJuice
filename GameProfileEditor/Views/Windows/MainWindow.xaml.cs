using GameProfileEditor.ViewModels;
using Mi5hmasH.WpfHelper.ControlProperties;
using System.Windows;
using System.Windows.Interop;
using Mi5hmasH.WpfHelper;

namespace GameProfileEditor.Views.Windows;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = new MainWindowViewModel();
        DataContext = this;

        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // WINDOWS_10_DARK_THEME_FIX
        if (!DarkModeWin10Helper.IsWindows10GreaterThan1809()) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        DarkModeWin10Helper.FixImmersiveDarkMode(hwnd);
    }

    #region FILE_DROP

    private void FileDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data is not DataObject dataObject || !dataObject.ContainsFileDropList()) return;
        if (sender is not UIElement element) return;
        var dropOperationType = DropProperties.GetDropOperationType(element);
        ViewModel.OnFileDrop(dropOperationType, dataObject.GetFileDropList());
    }

    private void FileDrop_PreviewDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data is not DataObject dataObject || !dataObject.ContainsFileDropList()) return;
        e.Effects = DragDropEffects.Copy;
    }

    private void FileDrop_PreviewDragOver(object sender, DragEventArgs e) => e.Handled = true;

    #endregion
}