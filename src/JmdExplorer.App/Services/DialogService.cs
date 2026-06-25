using System.Windows;
using Microsoft.Win32;

namespace JmdExplorer.App.Services;

public sealed class DialogService : IDialogService
{
    public string? OpenFile(string filter, string title)
    {
        var dlg = new OpenFileDialog { Filter = filter, Title = title, CheckFileExists = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dlg = new OpenFolderDialog { Title = title };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    public string? SaveFile(string filter, string defaultName, string title)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = defaultName, Title = title };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public void ShowMessage(string message, string title, bool isError = false) =>
        MessageBox.Show(message, title, MessageBoxButton.OK,
            isError ? MessageBoxImage.Error : MessageBoxImage.Information);

    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
