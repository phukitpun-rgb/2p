# JMD Explorer

A professional Windows desktop tool for **honestly** analyzing and carving data from `.jmd`
binary files — with first-class support for files carrying a `Xenon Data Format v4s` header,
and a plugin architecture so real decoders can be added later.

> **Design principle:** JMD Explorer never pretends it "unpacked" a file it cannot truly decode.
> It cleanly separates *recognizing structure* from *decoding an asset*, and it will never write
> a fake `.png`/`.fbx`/etc.

| Tech | |
|------|---|
| Runtime | .NET 8 |
| Language | C# |
| UI | WPF (MVVM) |
| MVVM toolkit | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Theme | Dark "developer / game-asset inspector" |

---

## The five honest states

Every analysis maps to one of these (`DecodeStatus`), and the UI shows exactly which one:

1. **Structure recognized** — the file layout/header was understood.
2. **Embedded signature found** — a known magic (PNG, ZIP, …) is present inside.
3. **Raw block carved** — exact bytes were extracted to `.bin` (meaning unverified).
4. **Asset decoded** — a genuine, usable asset was produced *(requires a real decoder)*.
5. **Decoder unavailable** — inspection only; no decoder exists for this format.

---

## Project structure

```
JmdExplorer.sln
src/
  JmdExplorer.Core/            # Models, interfaces, pure analysis services (no UI)
  JmdExplorer.Infrastructure/  # Streamed file IO, SHA-256, logging, report writers
  JmdExplorer.Decoders/        # IFormatProfile + IJmdDecoderPlugin implementations
  JmdExplorer.App/             # WPF app: DI root, Views, ViewModels, Dark theme
  JmdExplorer.Tests/           # xUnit tests
tools/
  SampleJmdGenerator/          # Generates sample .jmd fixtures for testing
docs/
  format-notes.md              # Observations about Xenon v4s (inspection only)
  plugin-guide.md              # How to add a profile / decoder
  known-limitations.md         # What it does NOT do, by design
README.md
build.cmd / run.cmd            # Windows convenience scripts
```

---

## Prerequisites

- Windows 10/11
- **.NET 8 SDK** — install from <https://aka.ms/dotnet/download> (or via `winget install Microsoft.DotNet.SDK.8`)

Verify:

```cmd
dotnet --version    REM should print 8.x
```

---

## Build & run (Windows)

### Option A — Visual Studio 2022
1. Open `JmdExplorer.sln`.
2. Set **JmdExplorer.App** as the startup project.
3. Press **F5**.

### Option B — command line

```cmd
REM restore + build everything
dotnet build JmdExplorer.sln -c Release

REM run the app
dotnet run --project src/JmdExplorer.App -c Release
```

Or use the helper scripts from the repo root:

```cmd
build.cmd       REM restore, build, and run the unit tests
run.cmd         REM launch the WPF app
```

### Generate sample files to try

```cmd
dotnet run --project tools/SampleJmdGenerator -- samples
```

This writes `samples/carinfo.jmd` (a real `Xenon Data Format v4s` header + metadata + embedded
PNG signature + a 64-byte repeating record table + a high-entropy payload), plus
`unknown_blob.jmd` and `strings_demo.bin`.

### Run the tests

```cmd
dotnet test
```

---

## Build a standalone `.exe`

Framework-dependent (smallest; needs .NET 8 runtime on the target machine):

```cmd
dotnet publish src/JmdExplorer.App -c Release -r win-x64 --self-contained false
```

Self-contained single file (no runtime needed on target):

```cmd
dotnet publish src/JmdExplorer.App -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The executable is produced at:

```
src/JmdExplorer.App/bin/Release/net8.0-windows/win-x64/publish/JmdExplorer.exe
```

---

## Screens & workflow

```
┌────────────┬──────────────────────────────────────────────┐
│ Sidebar    │  Center (selected page)        │ Detail panel │
│            │                                │              │
│ File Insp. │  File metadata + SHA-256       │ Detection &  │
│ Hex Viewer │  + honesty status lines        │ decode badge │
│ Structure  │                                │              │
│ Embedded   │                                │              │
│ Strings    │                                │              │
│ Extracted  │                                │              │
│ Decoders   │                                │              │
│ Export     │                                │              │
│ Settings   │                                │              │
└────────────┴──────────────────────────────────────────────┘
```

Typical workflow:

1. **Open File** (or drag a `.jmd` onto the window). The default filter is `*.jmd`, but any
   binary can be opened. Files are read via streams — they are *not* loaded fully into RAM.
2. **File Inspector** shows name, path, size, SHA-256, dates, header text, detected format,
   confidence, and the honest status lines, e.g.:
   ```
   Detected: Xenon Data Format v4s
   Status: Structure recognized, decoder unavailable
   Embedded assets: Not scanned yet
   Payload region: Run Structure Analyzer to detect
   Extraction result: Nothing extracted yet
   ```
3. **Structure Analyzer → Analyze** detects regions (entropy/zero-fill based, not hard-coded)
   and repeating-record candidates. Click **Hex** on a region to jump to it.
4. **Embedded File Scanner → Scan** lists PNG/JPG/DDS/ZIP/… signatures (or the honest
   "no signatures found" message). **Extract Raw** carves bytes only.
5. **Hex Viewer** — go-to-offset, search hex (`58 65 6E 6F 6E`) / text (`Xenon`), and copy a
   selection as Hex / ASCII / C# byte[] / Base64.
6. **String Scanner** — ASCII / UTF-8 / UTF-16 LE / UTF-16 BE with length, encoding, keyword,
   printable-only filters.
7. **Extracted Files** — extract a manual range or all detected regions to `.bin` + a JSON
   manifest (naming: `carinfo_payload_0x00012C00_EOF.bin`).
8. **Decoder Plugins** — lists `Generic Signature Scanner` (Enabled), `Xenon v4s Inspector`
   (Enabled), and `Xenon v4s Decoder` (**Not Available** — future plugin).
9. **Export Report** — TXT / JSON / HTML, including the limitation wording.

All long scans show progress and can be **cancelled**. Errors (missing/locked/too-large/denied
files, plugin crashes) are handled gracefully and logged to `logs/app-yyyyMMdd.log`; the app
does not crash.

---

## Command-line extractor (`jmdextract`)

For batch/headless use there is a small CLI in `tools/JmdExtract` that reuses the same Core
scanner and extractor as the app. It is ideal for pulling textures out of the `J2m Data
Format 1.0` files (~98% of a RayCity `data/` tree), which store **DDS textures in plaintext**.

```cmd
REM show header, size, SHA-256
dotnet run --project tools/JmdExtract -c Release -- info  path\to\car.jmd

REM scan and list embedded signatures (offset, size, confidence)
dotnet run --project tools/JmdExtract -c Release -- list  path\to\car.jmd

REM extract embedded files (DDS become real .dds; unknown carves are .bin)
dotnet run --project tools/JmdExtract -c Release -- extract path\to\car.jmd [outDir]
dotnet run --project tools/JmdExtract -c Release -- extract path\to\car.jmd outDir --all --png

REM batch: extract every *.jmd under a folder (recursive), one subfolder per file
dotnet run --project tools/JmdExtract -c Release -- batch path\to\data [outDir]

REM config: export car/stat config blocks as .xml (J2m "param" trees, e.g. ai.jmd)
dotnet run --project tools/JmdExtract -c Release -- config path\to\ai.jmd [outDir]
```

`config` reconstructs the per-car XML the game tools export — the same `<BD_Mass>`,
`<defaultPaint>`, `<cameraOffset>` structure — from the serialized property tree stored in
files like `ai.jmd` (one `.xml` per car, named by its emblem).

`--png` decodes DXT-compressed DDS textures to universally-viewable `.png`. `batch` turns
PNG on by default (use `--no-png` to skip) and writes one output subfolder per source file,
so you can unpack an entire `data/` tree (thousands of `.jmd`) in one command.

By default `extract` keeps assets with a header-derived size (e.g. DDS) plus high-confidence
matches, skipping noisy 2-byte-magic false positives; `--all` carves every signature; `--png`
also decodes DXT-compressed DDS textures to universally-viewable `.png`. A `*_manifest.json` is
written alongside the output. DDS sizes are computed from the real header so the carved `.dds`
files are complete and openable.

### GUI app (`JmdExtractApp`)

A point-and-click WinForms front-end lives in `tools/JmdExtractApp`. Open a `.jmd` (or drag it
onto the window), **Scan** to list embedded assets, select a DDS row to see an inline **preview**,
then **Extract All / Extract Selected** (with an optional "save DDS as PNG"). **Batch Folder…**
unpacks every `.jmd` under a chosen folder in one go, and **Export XML** writes the car/stat
config blocks (e.g. from `ai.jmd`) as `.xml`. It builds and publishes reliably from the CLI
(unlike the WPF app):

```cmd
dotnet run --project tools/JmdExtractApp -c Release
```

> **Honesty note:** `Xenon Data Format v4` files (e.g. `wheel.jmd`, `car.jmd`) use a packed
> payload that is **not yet decoded** — `jmdextract` will report what it sees but cannot
> extract genuine assets from them. See [docs/format-notes.md](docs/format-notes.md).

## Adding a real decoder in the future

The whole point of the architecture is that you can add real decoders without touching the UI:

- Recognize a new format → implement **`IFormatProfile`**.
- Actually decode content → implement **`IJmdDecoderPlugin`** and replace the placeholder
  `XenonV4sDecoderPlugin` (currently `Version = "N/A"`, `CanDecode => false`).

Register either in `App.xaml.cs` with one `AddSingleton(...)` line. A real decoder must produce
genuine asset files and report `DecodeStatus.AssetDecoded` **only** when it truly did so. See
[docs/plugin-guide.md](docs/plugin-guide.md) and [docs/format-notes.md](docs/format-notes.md).

---

## What this app deliberately does NOT do

See [docs/known-limitations.md](docs/known-limitations.md). In short: no fake decoded output,
ever. Carved blocks are raw bytes and are not guaranteed to be usable assets.
