namespace JmdExplorer.App.ViewModels;

/// <summary>A navigable page shown in the center column and listed in the sidebar.</summary>
public interface IPageViewModel
{
    string Title { get; }

    /// <summary>Segoe MDL2 Assets glyph code for the sidebar icon.</summary>
    string Glyph { get; }
}
