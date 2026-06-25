namespace JmdExplorer.App.Services;

/// <summary>Abstracts file/folder pickers and message boxes so VMs stay testable.</summary>
public interface IDialogService
{
    string? OpenFile(string filter, string title);
    string? PickFolder(string title);
    string? SaveFile(string filter, string defaultName, string title);
    void ShowMessage(string message, string title, bool isError = false);
    bool Confirm(string message, string title);
}
