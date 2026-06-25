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

## Empirical findings from a real sample (carinfo.jmd, 1,172,500 bytes)

Reverse-engineered by inspection only — **not a confirmed spec**. What was established:

- **Header**: `Xenon Data Format v4s` stored as **UTF-16LE** at offset `0x0` (not ASCII).
- **Pointer / index region** (`~0x1000–0x3000`): arrays of 32-bit little-endian values of the
  form `0x56F9xxxx` (and a second class `0x6F27xxxx`). These look like **absolute memory
  pointers** from a serialized in-memory object graph (base ≈ `0x56F90000`), i.e. the file is a
  *cooked memory snapshot* rather than a tagged/streamed format.
- **Zero padding**: a large `0x00` run (~`0x3000–0x12000`).
- **Main payload** (`0x13000 → EOF`): **17,104 fixed records of 64 bytes** (confirmed by
  autocorrelation: ~51% byte equality at distance 64, ~0–2% elsewhere). Per-column statistics:
  byte 0/2 of each 32-bit field are full-range random; byte 1/3 are limited (~70–110 distinct,
  strong mode). Adjacent records are ~50% byte-identical (slowly varying / delta-like).

What it is **NOT** (each tested and ruled out):

- Not zlib/deflate (all 17 `78 9C`/`78 01` hits fail to inflate → false positives).
- Not simple repeating-XOR: 64-byte column-mode key recovery only drops entropy 7.66→7.15 and
  yields ~10% zeros (a real key would collapse the constant columns).
- Not plain FP32 or FP16 vertex floats: FP32 gives ~1e29 garbage; FP16 decodes 97% finite but the
  values span the entire FP16 range with no sensible bounding box (looks random, not geometry).
- No readable ASCII/UTF-8 strings or asset paths anywhere (uses hashes/pointers, not names).

**Honest conclusion:** the container structure is well characterized, but the 64-byte record
encoding is a custom/packed scheme that cannot be turned into usable assets from a single sample
without the originating program's struct/vertex definitions (or a known-good reference output).
This is exactly the case the app refuses to fake.

**To finish a real decoder you would need one of:** the game/tool's serialization or vertex
declaration; a second sample plus its known decoded output (known-plaintext); or the executable
that reads these files (to recover the record layout and any packing/relocation step).

## Where a real decoder would plug in

Implement `IJmdDecoderPlugin` (see [plugin-guide.md](plugin-guide.md)) and replace the
placeholder [`XenonV4sDecoderPlugin`](../src/JmdExplorer.Decoders/Plugins/XenonV4sDecoderPlugin.cs).
A real decoder must produce genuine asset files and report `DecodeStatus.AssetDecoded` **only**
when it has actually done so.
