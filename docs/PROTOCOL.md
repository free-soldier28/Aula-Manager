# AULA F75 — Protocol Notes (field-tested)

Hardware: AULA F75, wired USB, SinoWealth chipset, VID `258A:010C`.
Reference model: AULA F87 (same chipset, same VID/PID).
All findings below were verified on a live F75 unless marked otherwise.

## Feature Report

- HID Feature Report, **Report ID 6**, vendor interface (usage_page `0xFF00`/`0xFF13`).
- Frame: `06 CMD A0 A1 A2 A3 L0 L1 <data...>`
  - `0x04` write config, `0x84` read config
  - `0x0A` write color profile, `0x8A` read color profile, `0x82` model query
- Response to `0x82` — 14 bytes: `06 82 01 00 01 00 06 00 03 00 00 00 03 66`
  - Model `0x03` (F75), `psd 00:CD`.

## Read/Write mechanics

- Read: first `SET_FEATURE` (the request), then `GET_FEATURE` (report 6).
- **GetFeature requires the report ID pre-filled in the buffer**: before reading,
  set `response[0] = Model.ReportId` (`0x06`), otherwise HidSharp throws.
- Response to `0x84` — 136 bytes (8-byte header + 128-byte payload).
- Writes to the config area **persist to keyboard flash immediately** (survive replug).

## Config layout (offsets in the 128-byte payload)

- effect — offset 18, custom mode — offset 17, side light — offset 26, battery — offset 36.
- per-effect params at `64 + 2*effect_id`: brightness, then `speed<<4 | flags`.
- **Brightness range is 0–9** (factory config shows `0x09`). Speed 0–4.
- Flags nibble: `0x7` = colorful, `0x0` = single color; other values select palettes.

## Color profile (`0x0A`/`0x8A`)

- RGB of the primary color lives on **payload bytes 29–31** (three bytes).
- Terminator `5A A5` at `0x202/0x203`.
- **This is NOT a per-key table.** Filling bytes 8+ with a per-key RGB layout
  (4 bytes per key, 128 keys) corrupted the color — green request turned red.
  Only bytes 29–31 are honored; per-key custom mode (`0x21`) stays dark without a
  committed custom table (commit/latch command still unknown).

## Effect map (verified on live F75)

Checked with camera analysis (p99 brightness / fraction of bright pixels) plus
manual key-press tests.

| id | name (CLI) | behavior | notes |
|---|---|---|---|
| 0 | off | off | |
| 1 | static | solid color | color from profile bytes 29–31 |
| 2 | breathing | pulsing | |
| 3 | wave | wave | |
| 4 | spectrum | **reactive** | lights only on key press |
| 5 | ripple | animates | |
| 6 | reactive | smooth color shift | NOT press-reactive |
| 7 | starlight | **reactive** | ripples out from pressed key |
| 8 | rain | animates | |
| 9 | snake | dark always | no light even on press (single-color) |
| 10 | marquee | moving band | |
| 11 | aurora | bright | |
| 12 | laser | **reactive** | pressed key glows random color, fades |
| 13 | firework | animates | |
| 14 | gradient | **single-color only** | dark with colorful flag, lights with `--color` |
| 15 | rainbow_wave | bright | |
| 16 | prism | bright | |
| 17 | cycle | bright | |
| 18 | tidal | mostly dark | barely lights with single-color |
| 19 | — | **does not exist** | CLI error |
| 20 | — | **does not exist** | CLI error |
| 21 | custom | **works** | per-key via cmd `0x06` + custom mode=1 (see below) |

Notes:
- Reactive effects (4, 7, 12) appear dark to a static camera; they light up on
  key press. This is why the earlier camera sweep mislabeled them as dead.
- Effects 9, 18 are effectively dead regardless of flags; 14 needs a single color.

## Per-key custom mode (effect 21) — SOLVED

Per-key RGB uses a **separate command `0x06`** (planar layout), not `0x0A`
(that is the single-color profile). Verified on live F75 with the camera and
manual key checks.

- Sequence: write config with effect 21 + custom mode = 1 (`0x04`), then send
  the per-key color table (`0x06`). The table **persists in flash** (survives
  replug; keyboard briefly shows a startup effect first, then the custom table).
- Command `0x06` layout (520-byte report, report id 6):
  - `[0]=0x06 [1]=0x06 [4]=0x01 [6]=ledCount(126) [7]=0x01`
  - bytes `0x08..0x85`: R channel, LED index 0..125
  - bytes `0x86..0x103`: G channel
  - bytes `0x104..0x181`: B channel
- F75 LED layout matches the F87 key map (88 keys, indices 0–101, e.g.
  esc=0, f4=24, w=14, space=35, enter=81). CLI: `aula perkey w=ff0000 a=00ff00`.
- The `0x0A` color profile remains the single primary color used by effect 1
  (static) and LED[0] (report bytes 29–31).

## Open questions

- Length of GET_FEATURE response on Linux (14 vs 520 bytes) — `HIDIOCGFEATURE`.
- Meaning of remaining palette nibbles (`0x1`, `0x2`, `0x4`, `0x6`).
