using System.Security.Cryptography;
using System.Text;
using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;

namespace JmdExtractApp;

/// <summary>
/// A minimal Windows GUI front-end for the JMD extractor. It opens a .jmd file,
/// scans for embedded asset signatures, and extracts them — DDS textures as real
/// .dds files (exact size parsed from the header), unknown matches carved raw as
/// .bin. It never fabricates an asset it could not size from a real header.
/// </summary>
public sealed class MainForm : Form
{
    private readonly TextBox _pathBox;
    private readonly Button _openBtn;
    private readonly Button _batchBtn;
    private readonly Button _scanBtn;
    private readonly Label _infoLabel;
    private readonly DataGridView _grid;
    private readonly PictureBox _previewBox;
    private readonly Label _previewLabel;
    private readonly CheckBox _lowConfChk;
    private readonly CheckBox _pngChk;
    private readonly Button _extractAllBtn;
    private readonly Button _extractSelBtn;
    private readonly ProgressBar _progress;
    private readonly Label _statusLabel;

    private string? _filePath;
    private IReadOnlyList<EmbeddedSignatureMatch> _matches = Array.Empty<EmbeddedSignatureMatch>();

    private static readonly Color BgDark = Color.FromArgb(30, 31, 34);
    private static readonly Color BgPanel = Color.FromArgb(43, 45, 49);
    private static readonly Color Fg = Color.FromArgb(222, 224, 228);
    private static readonly Color Accent = Color.FromArgb(86, 156, 214);

    public MainForm()
    {
        Text = "JMD Extractor";
        Width = 920;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgDark;
        ForeColor = Fg;
        Font = new Font("Segoe UI", 9f);
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        // --- Top row: open + batch + path + scan ---
        _openBtn = MakeButton("Open .jmd…", 12, 12, 100);
        _openBtn.Click += (_, _) => PickFile();

        _batchBtn = MakeButton("Batch Folder…", 116, 12, 110);
        _batchBtn.Click += async (_, _) => await BatchFolderAsync();

        _pathBox = new TextBox
        {
            Left = 234, Top = 14, Width = 536, ReadOnly = true,
            BackColor = BgPanel, ForeColor = Fg, BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _scanBtn = MakeButton("Scan", 780, 12, 110);
        _scanBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _scanBtn.Enabled = false;
        _scanBtn.Click += async (_, _) => await ScanAsync();

        // --- Info label ---
        _infoLabel = new Label
        {
            Left = 12, Top = 48, Width = 878, Height = 56, ForeColor = Color.Gray,
            Text = "Open a .jmd file (or drag one onto the window) to begin.",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // --- Grid (left) ---
        _grid = new DataGridView
        {
            Left = 12, Top = 110, Width = 560, Height = 410,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
            BackgroundColor = BgPanel, BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true,
            RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = BgDark;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Fg;
        _grid.DefaultCellStyle.BackColor = BgPanel;
        _grid.DefaultCellStyle.ForeColor = Fg;
        _grid.DefaultCellStyle.SelectionBackColor = Accent;
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        _grid.Columns.Add("idx", "#");
        _grid.Columns.Add("type", "Type");
        _grid.Columns.Add("offset", "Offset");
        _grid.Columns.Add("size", "Size");
        _grid.Columns.Add("conf", "Confidence");
        _grid.Columns[0].FillWeight = 30;
        _grid.SelectionChanged += (_, _) => UpdatePreview();

        // --- Preview (right) ---
        _previewLabel = new Label
        {
            Left = 584, Top = 110, Width = 306, Height = 20, ForeColor = Color.Gray,
            Text = "Preview", Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _previewBox = new PictureBox
        {
            Left = 584, Top = 132, Width = 306, Height = 388,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.FromArgb(20, 21, 24), BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        // --- Bottom controls ---
        _lowConfChk = new CheckBox
        {
            Text = "Include low-confidence matches", Left = 12, Top = 532, Width = 250,
            ForeColor = Fg, Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _lowConfChk.CheckedChanged += (_, _) => RefreshGrid();

        _pngChk = new CheckBox
        {
            Text = "Also save DDS as PNG", Left = 270, Top = 532, Width = 200, Checked = true,
            ForeColor = Fg, Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        _extractSelBtn = MakeButton("Extract Selected", 560, 528, 150);
        _extractSelBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _extractSelBtn.Enabled = false;
        _extractSelBtn.Click += async (_, _) => await ExtractAsync(selectedOnly: true);

        _extractAllBtn = MakeButton("Extract All", 720, 528, 170);
        _extractAllBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _extractAllBtn.Enabled = false;
        _extractAllBtn.Click += async (_, _) => await ExtractAsync(selectedOnly: false);

        // --- Status bar ---
        _progress = new ProgressBar
        {
            Left = 12, Top = 572, Width = 300, Height = 18,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Visible = false
        };
        _statusLabel = new Label
        {
            Left = 320, Top = 572, Width = 570, Height = 18, ForeColor = Color.Gray,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        Controls.AddRange(new Control[]
        {
            _openBtn, _batchBtn, _pathBox, _scanBtn, _infoLabel, _grid, _previewLabel, _previewBox,
            _lowConfChk, _pngChk, _extractSelBtn, _extractAllBtn, _progress, _statusLabel
        });
    }

    private static Button MakeButton(string text, int x, int y, int w)
    {
        var b = new Button
        {
            Text = text, Left = x, Top = y, Width = w, Height = 28,
            FlatStyle = FlatStyle.Flat, BackColor = BgPanel, ForeColor = Fg, UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(70, 72, 78);
        return b;
    }

    // --- File selection -------------------------------------------------

    private void OnDragEnter(object? s, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? s, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            LoadFile(files[0]);
    }

    private void PickFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Open a .jmd file",
            Filter = "JMD files (*.jmd)|*.jmd|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            LoadFile(dlg.FileName);
    }

    private void LoadFile(string path)
    {
        _filePath = path;
        _pathBox.Text = path;
        _matches = Array.Empty<EmbeddedSignatureMatch>();
        _grid.Rows.Clear();
        _extractAllBtn.Enabled = _extractSelBtn.Enabled = false;
        _scanBtn.Enabled = true;

        try
        {
            var fi = new FileInfo(path);
            string header = ReadHeaderText(path);
            bool known = header.StartsWith("Xenon", StringComparison.OrdinalIgnoreCase)
                      || header.StartsWith("J2m", StringComparison.OrdinalIgnoreCase)
                      || header.StartsWith("Scx", StringComparison.OrdinalIgnoreCase);
            _infoLabel.ForeColor = Fg;
            _infoLabel.Text =
                $"File: {fi.Name}    Size: {fi.Length:n0} bytes (0x{fi.Length:X})\n" +
                $"Header: {(string.IsNullOrEmpty(header) ? "(no text header)" : header)}" +
                $"    {(known ? "✓ recognized container" : "unrecognized header")}\n" +
                "Click Scan to find embedded assets (DDS textures are extractable).";
            _statusLabel.Text = "Ready. Click Scan.";
        }
        catch (Exception ex)
        {
            _infoLabel.ForeColor = Color.IndianRed;
            _infoLabel.Text = $"Could not read file: {ex.Message}";
        }
    }

    // --- Scan -----------------------------------------------------------

    private async Task ScanAsync()
    {
        if (_filePath is null) return;
        SetBusy(true, "Scanning for embedded signatures…");
        string path = _filePath;
        try
        {
            var matches = await Task.Run(() =>
            {
                var scanner = new SignatureScanner();
                using var fs = File.OpenRead(path);
                return scanner.Scan(fs, fs.Length, CancellationToken.None,
                    new Progress<double>(p => BeginInvoke(() => _progress.Value = (int)(p * 100))));
            });
            _matches = matches;
            RefreshGrid();
            int dds = matches.Count(m => m.Type == "DDS");
            _statusLabel.Text = $"Found {matches.Count} signature(s), {dds} DDS texture(s).";
            _extractAllBtn.Enabled = _extractSelBtn.Enabled = matches.Count > 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Scan failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Scan failed.";
        }
        finally { SetBusy(false); }
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        if (_matches.Count == 0) return;
        var view = Filtered(_matches).ToList();
        int i = 0;
        foreach (var m in view)
        {
            string size = m.SizeEstimate is > 0 ? $"{m.SizeEstimate:n0}" : "unknown";
            _grid.Rows.Add(i++, m.Type, m.OffsetHex, size, m.Confidence.ToString());
        }
        _statusLabel.Text = $"Showing {view.Count} of {_matches.Count} match(es).";
    }

    /// <summary>Default view: assets with a parsed size or High confidence. The
    /// checkbox shows the noisy 2-byte-magic matches too.</summary>
    private IEnumerable<EmbeddedSignatureMatch> Filtered(IEnumerable<EmbeddedSignatureMatch> src) =>
        _lowConfChk.Checked
            ? src
            : src.Where(m => m.SizeEstimate is > 0 || m.Confidence == Confidence.High);

    // --- Preview --------------------------------------------------------

    /// <summary>Decodes the selected DDS texture and shows it in the preview pane.</summary>
    private void UpdatePreview()
    {
        _previewBox.Image?.Dispose();
        _previewBox.Image = null;

        if (_filePath is null || _grid.SelectedRows.Count == 0) { _previewLabel.Text = "Preview"; return; }
        var view = Filtered(_matches).ToList();
        int idx = (int)_grid.SelectedRows[0].Cells[0].Value;
        if (idx < 0 || idx >= view.Count) return;
        var m = view[idx];

        if (m.Type != "DDS" || m.SizeEstimate is not > 0)
        {
            _previewLabel.Text = $"Preview — {m.Type} (no image preview)";
            return;
        }
        try
        {
            byte[] dds = new byte[m.SizeEstimate.Value];
            using (var fs = File.OpenRead(_filePath))
            {
                fs.Seek(m.Offset, SeekOrigin.Begin);
                fs.ReadExactly(dds);
            }
            if (!DdsImage.IsSupported(dds)) { _previewLabel.Text = "Preview — unsupported DDS format"; return; }
            var img = DdsImage.Decode(dds);
            _previewBox.Image = ToBitmap(img);
            _previewLabel.Text = $"Preview — {img.Width}×{img.Height} DDS";
        }
        catch (Exception ex)
        {
            _previewLabel.Text = $"Preview — decode failed: {ex.Message}";
        }
    }

    private static Bitmap ToBitmap(DdsImage.Decoded img)
    {
        var bmp = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, img.Width, img.Height);
        var bits = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
        try
        {
            // Source is RGBA; GDI+ Format32bppArgb is BGRA in memory, so swap R/B.
            byte[] bgra = new byte[img.Rgba.Length];
            for (int i = 0; i < img.Rgba.Length; i += 4)
            {
                bgra[i] = img.Rgba[i + 2];     // B
                bgra[i + 1] = img.Rgba[i + 1]; // G
                bgra[i + 2] = img.Rgba[i];     // R
                bgra[i + 3] = img.Rgba[i + 3]; // A
            }
            System.Runtime.InteropServices.Marshal.Copy(bgra, 0, bits.Scan0, bgra.Length);
        }
        finally { bmp.UnlockBits(bits); }
        return bmp;
    }

    // --- Extract --------------------------------------------------------

    private async Task ExtractAsync(bool selectedOnly)
    {
        if (_filePath is null) return;

        List<EmbeddedSignatureMatch> targets;
        if (selectedOnly)
        {
            var view = Filtered(_matches).ToList();
            targets = _grid.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => (int)r.Cells[0].Value)
                .Where(idx => idx >= 0 && idx < view.Count)
                .Select(idx => view[idx]).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show(this, "Select one or more rows first.", "Nothing selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }
        else
        {
            targets = Filtered(_matches).ToList();
        }

        using var dlg = new FolderBrowserDialog { Description = "Choose output folder" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        string outDir = dlg.SelectedPath;
        string path = _filePath;

        bool toPng = _pngChk.Checked;
        SetBusy(true, $"Extracting {targets.Count} file(s)…");
        try
        {
            var (outcome, pngCount) = await Task.Run(() =>
            {
                long len = new FileInfo(path).Length;
                string sha;
                using (var fs = File.OpenRead(path)) sha = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();

                var requests = targets.Select(m =>
                {
                    bool sizeKnown = m.SizeEstimate is > 0;
                    long size = sizeKnown ? m.SizeEstimate!.Value : Math.Min(64 * 1024, len - m.Offset);
                    return new ExtractionRequest
                    {
                        Name = $"{m.Type}_0x{m.Offset:X}",
                        StartOffset = m.Offset,
                        EndOffset = m.Offset + size,
                        Type = $"embedded_signature:{m.Type}",
                        Extension = sizeKnown ? ExtensionFor(m.Type) : "bin"
                    };
                }).ToList();

                var oc = new RawBlockExtractor().Extract(path, outDir, requests,
                    ReadHeaderText(path) is { Length: > 0 } h ? h : "Unknown binary", sha);
                int png = toPng ? ConvertDdsToPng(oc.BinFiles) : 0;
                return (oc, png);
            });

            int complete = targets.Count(m => m.SizeEstimate is > 0);
            _statusLabel.Text = $"Extracted {outcome.BinFiles.Count} file(s) ({complete} complete asset(s))" +
                                (toPng ? $", {pngCount} PNG." : ".");
            if (MessageBox.Show(this,
                    $"Extracted {outcome.BinFiles.Count} file(s) to:\n{outDir}\n\n" +
                    $"{complete} complete asset(s) with header-derived sizes; " +
                    $"{outcome.BinFiles.Count - complete} raw carve(s) as .bin.\n" +
                    (toPng ? $"{pngCount} DDS texture(s) also saved as .png.\n" : "") +
                    "\nOpen the folder now?",
                    "Extraction complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{outDir}\"");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Extraction failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Extraction failed.";
        }
        finally { SetBusy(false); }
    }

    // --- Batch folder ---------------------------------------------------

    /// <summary>Recursively extracts every *.jmd under a chosen folder into one subfolder
    /// each (textures as .dds + .png). Runs off the UI thread with progress.</summary>
    private async Task BatchFolderAsync()
    {
        using var inDlg = new FolderBrowserDialog { Description = "Choose a folder of .jmd files (searched recursively)" };
        if (inDlg.ShowDialog(this) != DialogResult.OK) return;
        string inFolder = inDlg.SelectedPath;

        using var outDlg = new FolderBrowserDialog { Description = "Choose the output folder" };
        if (outDlg.ShowDialog(this) != DialogResult.OK) return;
        string outRoot = outDlg.SelectedPath;

        SetBusy(true, "Scanning folder…");
        try
        {
            var files = await Task.Run(() =>
                Directory.EnumerateFiles(inFolder, "*.jmd", SearchOption.AllDirectories).ToList());
            if (files.Count == 0)
            {
                MessageBox.Show(this, "No .jmd files found in that folder.", "Batch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var (assets, png, failed) = await Task.Run(() =>
            {
                int a = 0, p = 0, fail = 0, done = 0;
                string baseFull = Path.GetFullPath(inFolder);
                foreach (var f in files)
                {
                    try
                    {
                        string rel = Path.GetRelativePath(baseFull, f);
                        string dest = Path.Combine(outRoot, Path.ChangeExtension(rel, null)!);
                        var (ex, pc) = ExtractFileToFolder(f, dest);
                        if (ex > 0) { a += ex; p += pc; }
                        else if (Directory.Exists(dest)) Directory.Delete(dest, true);
                    }
                    catch { fail++; }
                    done++;
                    int d = done;
                    BeginInvoke(() =>
                    {
                        _progress.Value = (int)(d * 100.0 / files.Count);
                        _statusLabel.Text = $"Batch: {d}/{files.Count} files, {a} textures…";
                    });
                }
                return (a, p, fail);
            });

            _statusLabel.Text = $"Batch done: {assets} texture(s), {png} PNG from {files.Count} file(s).";
            if (MessageBox.Show(this,
                    $"Processed {files.Count} .jmd file(s) ({failed} failed).\n" +
                    $"Extracted {assets} texture(s), {png} PNG to:\n{outRoot}\n\nOpen the folder now?",
                    "Batch complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                System.Diagnostics.Process.Start("explorer.exe", $"\"{outRoot}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Batch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false); }
    }

    /// <summary>Extracts useful assets from one .jmd into <paramref name="dest"/>, decoding
    /// DDS to PNG. Returns (filesExtracted, pngConverted). Used by batch mode.</summary>
    private static (int extracted, int png) ExtractFileToFolder(string path, string dest)
    {
        var scanner = new SignatureScanner();
        IReadOnlyList<EmbeddedSignatureMatch> matches;
        long len;
        using (var fs = File.OpenRead(path)) { len = fs.Length; matches = scanner.Scan(fs, fs.Length); }
        var useful = matches.Where(m => m.SizeEstimate is > 0 || m.Confidence == Confidence.High).ToList();
        if (useful.Count == 0) return (0, 0);

        var requests = useful.Select(m =>
        {
            bool known = m.SizeEstimate is > 0;
            long size = known ? m.SizeEstimate!.Value : Math.Min(64 * 1024, len - m.Offset);
            return new ExtractionRequest
            {
                Name = $"{m.Type}_0x{m.Offset:X}",
                StartOffset = m.Offset,
                EndOffset = m.Offset + size,
                Type = $"embedded_signature:{m.Type}",
                Extension = known ? ExtensionFor(m.Type) : "bin"
            };
        }).ToList();
        var outcome = new RawBlockExtractor().Extract(path, dest, requests, "jmd", null);
        return (outcome.BinFiles.Count, ConvertDdsToPng(outcome.BinFiles));
    }

    // --- Helpers --------------------------------------------------------

    private void SetBusy(bool busy, string? status = null)
    {
        _openBtn.Enabled = _batchBtn.Enabled = _scanBtn.Enabled = !busy;
        _extractAllBtn.Enabled = _extractSelBtn.Enabled = !busy && _matches.Count > 0;
        _progress.Visible = busy;
        _progress.Value = 0;
        if (status is not null) _statusLabel.Text = status;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private static string ReadHeaderText(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> head = stackalloc byte[64];
        int n = fs.Read(head);
        string text = Encoding.Unicode.GetString(head[..(n - (n % 2))]);
        int z = text.IndexOf('\0');
        return (z >= 0 ? text[..z] : text).Trim();
    }

    /// <summary>Decodes every carved .dds to a sibling .png; skips unsupported formats.</summary>
    private static int ConvertDdsToPng(IEnumerable<string> files)
    {
        int count = 0;
        foreach (var f in files.Where(f => f.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                byte[] dds = File.ReadAllBytes(f);
                if (!DdsImage.IsSupported(dds)) continue;
                var img = DdsImage.Decode(dds);
                PngWriter.WriteRgba(Path.ChangeExtension(f, ".png"), img.Width, img.Height, img.Rgba);
                count++;
            }
            catch { /* leave the .dds if decode fails */ }
        }
        return count;
    }

    // Real extension only when the exact, complete size was parsed from a header.
    private static string ExtensionFor(string type) => type switch
    {
        "DDS" => "dds",
        "PNG" => "png",
        "JPEG" => "jpg",
        "BMP" => "bmp",
        "WAV (RIFF)" => "wav",
        "OGG" => "ogg",
        "ZIP" => "zip",
        _ => "bin"
    };
}
