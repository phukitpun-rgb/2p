using System.Windows;
using JmdExplorer.App.ViewModels;

namespace JmdExplorer.App;

/// <summary>
/// Code-behind is intentionally thin: it only translates raw drag/drop events into a
/// call on the view model. All logic lives in <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            await vm.LoadFileAsync(files[0]);
        }
    }
}
