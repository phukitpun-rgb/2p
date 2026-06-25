# Plugin Guide — Adding a Decoder or Format Profile

JMD Explorer is built so new recognizers and decoders can be added **without touching the UI**.
There are two extension points.

---

## 1. `IFormatProfile` — recognize a new file format

```csharp
public interface IFormatProfile
{
    string Name { get; }
    bool CanHandle(BinaryReader reader);     // cheap header probe (stream at 0)
    FormatAnalysisResult Analyze(Stream stream);
}
```

Steps:

1. Create a class in `JmdExplorer.Decoders/Profiles/`.
2. In `CanHandle`, read a few header bytes and return quickly. **Never throw.**
3. In `Analyze`, return a `FormatAnalysisResult`. Set:
   - `Confidence` honestly (`High` only when the magic is unambiguous).
   - `DecoderAvailable = false` unless a verified decoder exists.
   - `Status` to the appropriate `DecodeStatus`.
   - Optionally `RegionHints` to help the Structure Analyzer label regions.
4. Register it in `App.xaml.cs`:

```csharp
sc.AddSingleton<IFormatProfile, MyNewFormatProfile>();
```

`FormatDetector` automatically picks the highest-confidence profile; `UnknownBinaryProfile`
is the guaranteed fallback.

---

## 2. `IJmdDecoderPlugin` — actually decode content

```csharp
public interface IJmdDecoderPlugin
{
    string Name { get; }
    string Version { get; }
    bool CanDecode(JmdFileContext context);
    DecodeResult Decode(JmdFileContext context);
}
```

Steps:

1. Create a class in `JmdExplorer.Decoders/Plugins/`.
2. `CanDecode` returns whether this plugin handles the file.
3. `Decode` returns a `DecodeResult`. **Honesty rules (enforced by convention):**
   - Return `DecodeStatus.EmbeddedSignatureFound` if you only located signatures.
   - Return `DecodeStatus.RawBlockCarved` if you only wrote raw bytes.
   - Return `DecodeStatus.AssetDecoded` **only** when you produced a genuine, usable asset
     (e.g. a real `.png`/`.fbx`) and you can stand behind it.
   - Never fabricate output. If you cannot decode, return `DecodeResult.Unsupported(...)`.
4. **Never throw** — wrap failures in a `DecodeResult`. The Decoder Plugins page also catches
   exceptions so a crashing plugin cannot take down the app, but defensive plugins are better.
5. Register it in `App.xaml.cs` (registration order = display order):

```csharp
sc.AddSingleton<IJmdDecoderPlugin, MyNewDecoderPlugin>();
```

---

## Replacing the placeholder Xenon decoder

[`XenonV4sDecoderPlugin`](../src/JmdExplorer.Decoders/Plugins/XenonV4sDecoderPlugin.cs) ships as
`Version = "N/A"` and `CanDecode => false`, so the UI lists it as **Not Available**. When the
container format is reverse engineered:

1. Implement real parsing in `Decode`.
2. Bump `Version` to e.g. `"1.0"` so the status flips to **Enabled**.
3. Produce real asset files and set `ProducedFile.IsRawBlock = false` for verified assets.

That is the only change required — DI discovery and the UI handle the rest.
