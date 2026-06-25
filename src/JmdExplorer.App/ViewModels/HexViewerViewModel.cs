using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Services;

namespace JmdExplorer.App.ViewModels;

/// <summary>
/// Paged hex viewer. It reads a window of bytes around the current page offset rather
/// than loading the whole file, so it stays responsive on multi-GB inputs.
/// </summary>
public sealed partial class HexViewerViewModel : ObservableObject, IPageViewModel,
    IRecipient<NavigateToHexOffsetMessage>
{
    private const int BytesPerLine = 16;
    private const int LinesPerPage = 64;            // 1 KiB per page
    private const int PageBytes = BytesPerLine * LinesPerPage;

    private readonly AppSession _session;
    private readonly IAppLogger _logger;

    public HexViewerViewModel(AppSession session, IAppLogger logger, IMessenger messenger)
    {
        _session = session;
        _logger = logger;
        _session.FileLoaded += (_, _) => OnFileLoaded();
        messenger.RegisterAll(this);
    }

    public string Title => "Hex Viewer";
    public string Glyph => ""; // code

    public ObservableCollection<HexLine> Lines { get; } = new();

    public bool HasFile => _session.Context is not null;

    [ObservableProperty] private long _pageOffset;
    [ObservableProperty] private string _pageOffsetText = "0x00000000";
    [ObservableProperty] private string _gotoOffsetInput = "";
    [ObservableProperty] private string _searchHexInput = "";
    [ObservableProperty] private string _searchTextInput = "";
    [ObservableProperty] private long _selectionStart = -1;
    [ObservableProperty] private int _selectionLength;
    [ObservableProperty] private string _statusMessage = "";

    private void OnFileLoaded()
    {
        OnPropertyChanged(nameof(HasFile));
        PageOffset = 0;
        RenderPage();
    }

    public void Receive(NavigateToHexOffsetMessage message)
    {
        SelectionStart = message.Offset;
        SelectionLength = message.SelectionLength;
        GoToOffset(message.Offset);
    }

    private void RenderPage()
    {
        Lines.Clear();
        var ctx = _session.Context;
        if (ctx is null) return;

        long start = Math.Clamp(PageOffset, 0, Math.Max(0, ctx.Length - 1));
        // Align to line boundary.
        start -= start % BytesPerLine;
        PageOffset = start;
        PageOffsetText = $"0x{start:X8}";

        byte[] window = _session.Files.ReadRange(ctx, start, PageBytes);
        for (int i = 0; i < window.Length; i += BytesPerLine)
        {
            int len = Math.Min(BytesPerLine, window.Length - i);
            var slice = new byte[len];
            Array.Copy(window, i, slice, 0, len);
            Lines.Add(new HexLine(start + i, slice));
        }
        StatusMessage = $"Showing 0x{start:X8} .. 0x{start + window.Length:X8} of 0x{ctx.Length:X8}";
    }

    [RelayCommand]
    private void NextPage()
    {
        if (_session.Context is null) return;
        if (PageOffset + PageBytes < _session.Context.Length)
        {
            PageOffset += PageBytes;
            RenderPage();
        }
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (PageOffset <= 0) return;
        PageOffset = Math.Max(0, PageOffset - PageBytes);
        RenderPage();
    }

    [RelayCommand]
    private void Goto()
    {
        long? off = ParseOffset(GotoOffsetInput);
        if (off is null)
        {
            StatusMessage = "Invalid offset. Use decimal (4096) or hex (0x1000).";
            return;
        }
        GoToOffset(off.Value);
    }

    private void GoToOffset(long offset)
    {
        if (_session.Context is null) return;
        PageOffset = Math.Clamp(offset, 0, Math.Max(0, _session.Context.Length - 1));
        RenderPage();
    }

    [RelayCommand]
    private void SearchHex()
    {
        var pattern = ByteFormatter.ParseHexPattern(SearchHexInput);
        if (pattern is null) { StatusMessage = "Invalid hex pattern."; return; }
        FindAndGo(pattern);
    }

    [RelayCommand]
    private void SearchText()
    {
        if (string.IsNullOrEmpty(SearchTextInput)) return;
        var pattern = System.Text.Encoding.ASCII.GetBytes(SearchTextInput);
        FindAndGo(pattern);
    }

    private void FindAndGo(byte[] pattern)
    {
        var ctx = _session.Context;
        if (ctx is null) return;
        try
        {
            using var stream = ctx.OpenStream();
            long from = PageOffset + 1;
            long found = PatternSearch.FindFirst(stream, pattern, from);
            if (found < 0 && from > 0)
            {
                // wrap around
                using var s2 = ctx.OpenStream();
                found = PatternSearch.FindFirst(s2, pattern, 0);
            }
            if (found < 0) { StatusMessage = "Pattern not found."; return; }

            SelectionStart = found;
            SelectionLength = pattern.Length;
            GoToOffset(found);
            StatusMessage = $"Match at 0x{found:X8}";
        }
        catch (Exception ex)
        {
            _logger.Error("Hex search failed", ex);
            StatusMessage = $"Search error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyHex() => CopySelection(b => ByteFormatter.ToHexString(b));

    [RelayCommand]
    private void CopyAscii() => CopySelection(b => ByteFormatter.ToAsciiString(b));

    [RelayCommand]
    private void CopyCSharp() => CopySelection(b => ByteFormatter.ToCSharpByteArray(b));

    [RelayCommand]
    private void CopyBase64() => CopySelection(b => ByteFormatter.ToBase64(b));

    private void CopySelection(Func<byte[], string> formatter)
    {
        var ctx = _session.Context;
        if (ctx is null || SelectionStart < 0 || SelectionLength <= 0)
        {
            StatusMessage = "Nothing selected. Set offset + length or run a search first.";
            return;
        }
        try
        {
            byte[] bytes = _session.Files.ReadRange(ctx, SelectionStart, SelectionLength);
            Clipboard.SetText(formatter(bytes));
            StatusMessage = $"Copied {bytes.Length} byte(s) to clipboard.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
        }
    }

    private static long? ParseOffset(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(input.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out long h) ? h : null;
        }
        return long.TryParse(input, out long d) ? d : null;
    }
}
