# Format Notes — Xenon Data Format v4s (`.jmd`)

> These are **observations from inspection only**. JMD Explorer does not have a verified
> decoder for this format. Nothing here should be treated as a confirmed specification.

## Detection

A file is recognized as *Xenon Data Format v4s* when the ASCII byte sequence

```
58 65 6E 6F 6E 20 44 61 74 61 20 46 6F 72 6D 61 74 20 76 34 73   ("Xenon Data Format v4s")
```

appears within the first 512 bytes (normally at offset `0x00000000`). See
[`XenonDataFormatV4sProfile`](../src/JmdExplorer.Decoders/Profiles/XenonDataFormatV4sProfile.cs).

When matched, the profile reports:

```
Format     : Xenon Data Format v4s
Confidence : High
Decoder    : Not implemented
Mode       : Inspection only
Status     : Structure recognized (NOT decoded)
```

## What we can observe (heuristically)

The Structure Analyzer classifies regions purely from **per-block entropy** and **zero-fill
ratio** — it does **not** hard-code offsets. Typical Xenon v4s files tend to show:

| Region            | How it is detected                                    |
| ----------------- | ----------------------------------------------------- |
| Header            | Always the first block (contains the marker + meta)   |
| Metadata / index  | Low entropy (≤ ~4.0 bits/byte), often readable strings |
| Repeating records | Fixed-size rows with constant leading columns          |
| Payload           | High entropy (≥ ~7.2 bits/byte) — likely packed/compressed |
| Zero padding      | ≥ 97% zero bytes                                       |

A profile may supply *heuristic hints* (`FormatAnalysisResult.RegionHints`) that re-label a
detected region, but the boundaries themselves are always discovered statistically.

## What we explicitly do NOT know

- The exact container/table-of-contents layout.
- Whether the high-entropy payload is compressed, encrypted, or both.
- Any per-asset schema (models, textures, configs).

Until these are reverse engineered and verified, extraction yields **raw blocks only**.

## Where a real decoder would plug in

Implement `IJmdDecoderPlugin` (see [plugin-guide.md](plugin-guide.md)) and replace the
placeholder [`XenonV4sDecoderPlugin`](../src/JmdExplorer.Decoders/Plugins/XenonV4sDecoderPlugin.cs).
A real decoder must produce genuine asset files and report `DecodeStatus.AssetDecoded` **only**
when it has actually done so.
