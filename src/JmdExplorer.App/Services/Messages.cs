namespace JmdExplorer.App.Services;

/// <summary>Request to switch the active page to the Hex Viewer at a given offset.</summary>
public sealed record NavigateToHexOffsetMessage(long Offset, int SelectionLength);

/// <summary>Request to switch the active page by its page type name.</summary>
public sealed record NavigateToPageMessage(string PageTitle);
