# Known Limitations

JMD Explorer is intentionally an **honest inspector**, not a magic unpacker. These limitations
are by design.

## It does not decode proprietary payloads

- There is **no verified decoder** for *Xenon Data Format v4s* (or any unknown format).
- "Detected: Xenon Data Format v4s" means the **header was recognized**, not that the file
  was decoded.
- The Structure Analyzer's region types (Header / Metadata / Payload / …) are **heuristic
  classifications** from entropy and zero-fill statistics, not parsed structures.

## Extraction produces raw blocks only

- The Raw Block Extractor copies **exact bytes** of a chosen range to a `.bin` file and writes
  a JSON manifest. It performs **no decoding, decompression, or decryption**.
- A carved block:
  > is raw binary data and is **not guaranteed** to be directly usable as a model, texture,
  > or configuration file.

## Embedded signatures are hints, not proof

- Finding a PNG/ZIP/DDS/etc. signature means the **magic bytes** are present. It does **not**
  prove a complete, valid embedded file exists at that offset.
- Size estimates are only computed for formats with deterministic terminators (e.g. PNG `IEND`,
  RIFF size field). Otherwise size is reported as **Unknown** and extraction uses a conservative
  fixed window so nothing is fabricated.

## Heuristics can be wrong

- Repeating-record detection reports **candidates with confidence**, never confirmed schemas.
- Entropy thresholds are tuned for typical game assets and may misclassify unusual files.

## Practical limits

- Default maximum file size is 4 GiB (configurable in `FileServiceOptions`).
- The hex viewer pages through the file (1 KiB per page) rather than loading it all.
- Very large string/signature scans can take time; all scans are **cancellable**.

## What would remove these limitations

A verified `IJmdDecoderPlugin` implementation for the specific format (see
[plugin-guide.md](plugin-guide.md)). Only then will the app report `Asset decoded` and produce
real, usable asset files.
