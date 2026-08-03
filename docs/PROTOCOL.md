# AULA F75 — Protocol Notes (field-tested)

Hardware: AULA F75, wired USB, SinoWealth chipset, VID `258A:010C`.
Reference model: AULA F87 (same chipset, same VID/PID).
All findings below were verified on a live F75 unless marked otherwise.

## Wireless (2.4 GHz) — protocol 0x13 (field-verified)

The F75 is a tri-mode board (wired + 2.4 GHz dongle + Bluetooth). The 2.4 GHz
receiver uses a **completely different protocol** from the wired link: 20-byte
HID **output reports with Report ID `0x13`**, not feature reports. This is the
same family as the AULA F87 / F99 Pro (`3554:FA09`).

- Receiver: `VID 3554:PID FA09` ("2.4G Wireless Receiver"). Other PIDs in the
  `3554` family are a different (mouse) receiver and are NOT this keyboard.
- Wired: `VID 258A:PID 010C` (feature-report protocol, frame `0x06`, below).
- The wired frame-06 protocol (`GET_FEATURE`) **does not work over the dongle**:
  `dump` returns zeros, `col06` (feature=8) answers with the "canister" header
  `06 84 01/02/03` + zeros. All lighting must go over protocol `0x13`.

### Collection to use

The receiver exposes several HID interfaces. The lighting one is `col01` with
`input=20 output=20`. `DevicePicker.PickBest()` prefers a wireless collection
with `MaxOutputReportLength > 0` (highest first), falling back to feature-based
picking for wired. Verified live: READ returns valid checksummed fragments.

### Frame format

Every fragment is a 20-byte output report, Report ID `0x13`:

| offset | meaning |
|---|---|
| 0 | Report ID (`0x13`) |
| 1 | command |
| 2 | sub-command |
| 3 | sequence (0..N) |
| 4–18 | 15-byte payload |
| 19 | checksum = `sum(bytes[0..18]) & 0xFF` |

Every fragment sent by the host is **echoed back** by the receiver; the host
should read the echo before sending the next fragment.

Commands: `0x44` READ, `0x04` WRITE config, `0x09` color palette, `0x02`
per-key map, `0x0A` save. Sub-commands: `0x0A` config, `0x25` palette, `0x1C`
per-key, `0x01` confirm.

### Read config

- Send one `13 44 01 00` frame; the receiver echoes 10 fragments
  (`13 44 0A <seq> ...`, seq 0–9). Fragments are the config.
- Effect id lives in fragment 0 at `[15]`. The per-effect brightness/speed table
  lives in fragments 4–6 (`_effect_table_loc`):
  - effects 1–6 → fragment 4, offset `7 + (n-1)*2`
  - effects 7–13 → fragment 5, offset `5 + (n-7)*2`
  - effects 14–18 → fragment 6, offset `5 + (n-14)*2`
- Brightness 0–9, speed 0–4; speed-flags nibble: `0x7` colorful, `0x0` single color.

### Apply effect (4 phases)

1. **Read** 10 config fragments.
2. **Write config**: re-send each fragment with command `0x04`; on fragment 0 set
   `[8]=0x01` (write flag), `[14]=0x00` (apply), `[15]=effect id`,
   `[17]=0x01` if a color/colorful/per-key is used else `0x03`; on the effect's
   table fragment set brightness / speed byte; recompute checksum.
3. **Color palette**: 37 fragments (`0x09`/`0x25`). 21 from the factory template,
   zeros to fragment 35, `08 00 00 5A A5 ...` trailer at fragment 36. The custom
   color slot lives in fragment 1 payload `[8..10]` RGB + `[12]=0xFF` active.
   Sent on every non-per-key apply (matches the OEM app).
4. **Save**: single frame `13 0A 01 00 04 07 00...` (payload `04 07`).

Per-key mode: write config with effect 21 + `[17]=0x01`, then 28 per-key frames
(`0x02`/`0x1C`): 3 color planes × 9 fragments × 14 LEDs, each payload `0E` +
14 bytes, then a trailer `06 00 00 5A A5`. F75 LED map = F87 (126 LEDs,
indices 0–101, esc=0, f1=12 ... right=101).

### Verified commands over the dongle

`wireless read` (READ + fragment dump), `wireless effect <id> [--brightness]
[--speed] [--color] [--colorful]` — effect, brightness and speed confirmed via
read-back on a live F75. `perkey` and GUI tabs work through the wireless driver.

### Bluetooth — NOT supported by this protocol (field-verified)

- In BT mode the keyboard connects over **Bluetooth Classic (BR/EDR)** as
  "`AULA-F75 3.0 KB`" (BT 3.0 profile; the BLE 5.0 twin is "`AULA-F75 5.0 KB`").
  A BLE scanner (``bleak``) never sees it — it is a Classic-BT HID device.
- On Windows the BT link exposes a HID device `VID 3554 : PID FA08` (6 HID
  collections, `col01`–`col06`) — note the PID differs from the 2.4G dongle
  (`FA09`).
- **Every BT-HID interface reports `MaxFeatureReportLength = 0`** (checked with
  HidSharp enumeration while the keyboard was live in BT mode). Without a
  feature report there is no `SET_FEATURE`/`GET_FEATURE` transport at all, so
  the lighting protocol (report id `0x06`, CMD `0x0A/0x04`) **physically cannot
  be sent**.
- Consequence: RGB over Bluetooth needs the vendor driver or a low-level
  (L2CAP PSM 0x11 control-channel / proprietary GATT) reverse-engineering path;
  none exists in public open-source (all AULA projects drive wired + 2.4G only).
  Treat BT as unsupported.
- Practical rule: drive lighting in **wired or 2.4 GHz** mode only.

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
