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

---

## Empirical findings from real `.jmd` game files

These notes come from analysing a real RayCity `data/` tree (2,526 `.jmd` files). They are
still observations, not a verified spec, but they are confirmed against actual files.

### Three on-disk sub-formats (by UTF-16LE header text at offset 0)

| Header text             | Count        | Content                                              |
| ----------------------- | ------------ | ---------------------------------------------------- |
| `J2m Data Format 1.0`   | 2,476 (~98%) | **DDS textures stored in plaintext** — extractable  |
| `Xenon Data Format v4`  | 48           | High-entropy / packed payload — not yet decoded      |
| `Scx Data Format v3.0`  | 2            | Not yet analysed                                     |

The header text is UTF-16LE, zero-padded; bytes `0x2A–0x80` are zero in every file.

### J2m files — extractable today ✅

- Bulk content is **not encrypted**. The high entropy is just DXT-compressed pixel data.
- DDS assets sit at aligned offsets, preceded by zero padding, with valid `DDS_HEADER`
  (`dwSize=124`, power-of-two dimensions, `fourCC` = `DXT1`/`DXT3`, `caps=0x401008`).
- The exact length is computed from the header (128-byte header + every mip level). For
  block-compressed formats: per mip `ceil(w/4)*ceil(h/4)*blockBytes`, where
  `blockBytes = 8` for DXT1/BC1/BC4 and `16` otherwise.
- This math lands **exactly** on the trailing zero padding for every observed texture, so
  carving produces real, openable `.dds` files. See `EstimateDds` in
  [`DefaultSignatures`](../src/JmdExplorer.Core/Services/DefaultSignatures.cs).

### Xenon v4 files — still opaque 🔒

- `XOR` of two Xenon files cancels to a high zero-ratio **only for similar files**
  (8–52% across pairs), and the zero-padding zone stays zero. That rules out a simple
  fixed-keystream XOR over the whole file and points to **content-dependent packing
  (compression), not a trivial cipher**.
- AES-128-ECB and RC4 with the `XenonServices` license key (`config.ini`) both produce
  garbage on the payload, and `0x78xx` candidates do **not** inflate as zlib.
- The official `XenonFileSystem-10.2.exe` is **Themida-protected** (`.themida`/`.boot`
  sections, no readable strings), so it is not a practical reference for the algorithm.
- Decoding these 48 files needs further reverse engineering of the (likely custom)
  compression/container before any honest extraction is possible.

### Command-line extractor

A small companion CLI, [`tools/JmdExtract`](../tools/JmdExtract), reuses the Core scanner
and extractor to list and pull embedded assets from any `.jmd`. See the README for usage.
