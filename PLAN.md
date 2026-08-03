# AulaManager — Implementation Plan

A cross-platform **C# / .NET** application for managing AULA keyboard settings.

- **Target OS:** Windows, Linux (primary), macOS (future plan).
- **First model:** AULA F75 (wired USB), SinoWealth chipset / VID `258A:010C`.
- **Reference model:** AULA F87 (same SinoWealth chipset, same VID/PID) — confirms the framework's extensibility.
- **First feature:** key backlight and effects configuration.

## Stack

| Layer | Choice |
|---|---|
| Language / runtime | C# 12, .NET 8 LTS |
| HID | HidSharp 2.6.x (Windows HidP, Linux hidraw, macOS IOKit) |
| Profiles | System.Text.Json |
| Tests | xUnit |
| CLI | Aula.Cli (console, same core) |
| GUI (phase 8+) | Avalonia UI |

## Solution structure

```
AulaManager.slnx
├── src/
│   ├── Aula.Core/              # domain, protocol, drivers, services (net8.0, no UI)
│   │   ├── Abstractions/       # extensibility contracts (IAulaKeyboard, IKeyboardDriver, …)
│   │   ├── Devices/            # scanner, DeviceInfo, AulaDeviceIds
│   │   ├── Drivers/            # DriverRegistry, SinoSwellFeatureDriver, AulaKeyboard, transport
│   │   ├── Models/             # ModelConfig (F75/F87), KeyboardConfig, LedEffect, RgbColor
│   │   ├── Protocol/           # 06 frames, HidSharpTransport, SinowealthProtocol
│   │   └── Services/           # KeyboardDeviceFactory, LightingService
│   ├── Aula.Cli/               # console front-end
│   └── Aula.App/               # Avalonia UI (later)
├── tests/
│   ├── Aula.Core.Tests/        # xUnit (+ TestHelpers: FakeTransport, FakeScanner, FakeTransportFactory)
│   └── Aula.Cli.Tests/         # argument parser
├── packaging/
│   └── linux/99-aula-keyboard.rules
├── docs/
│   └── PROTOCOL.md             # documents the F75 protocol
└── PLAN.md
```

## Principles

1. Each phase ends with: implementation → tests → build/run → git commit.
2. The core (`Aula.Core`) knows nothing about the UI — CLI and GUI use the same services.
3. OS-specific behavior is isolated behind interfaces (`IHidTransport`).
4. Without a real keyboard, the logic is covered by unit tests on "golden" frames.
5. **Extensibility:** adding a new model = `ModelConfig` + registering a driver (SinoWealth chip), or a new `IKeyboardDriver` (different chip — SONiX, etc.). The app works only through `IAulaKeyboard`/`ILightingController`.

## Known facts about the F75/F87 protocol (confirmed by F87 reverse engineering)

- HID **Feature Report, Report ID 6, 520 bytes**, vendor interface (usage_page `0xFF00`/`0xFF13`).
- Frame: `06 CMD A0 A1 A2 A3 L0 L1 <data…>`
  - `0x04` — write configuration, `0x84` — read it
  - `0x0A` — write color profile (per-key colors), `0x82` — model query
  - config region: address `00 00 01 00`, length `0x0080`
  - color profile: RGB of the first key at bytes 29–31, terminator `5A A5` at `0x202/0x203`
  - response to `0x82` — 14 bytes: `06 82 01 00 01 00 06 00 03 00 00 00 03 66`
- Reading: first `SET_FEATURE` (request), then `GET_FEATURE` (6). Response to `0x84` — 136 bytes (8 header + 128 payload).
- Config response: effect — offset 18, custom mode — 17, side light — 26, battery — 36; effect parameters at `64 + 2×effect_id` (brightness, `speed<<4 | flags`).
- Effects table: **brightness 0–9** (factory config `0x09`), speed 0–4 — iterate via host rendering in the future.
- Writes to the config region are **saved to the keyboard flash immediately** (survive reboot).
- **Effect map on hardware:** reactive — 4 (spectrum), 7 (starlight), 12 (laser); single-color only — 14 (gradient); non-existent — 19, 20; custom 21 requires a per-key table (not implemented). Details in `docs/PROTOCOL.md`.
- **Open questions:** the "commit/latch" command for displaying per-key colors; GET_FEATURE response length on Linux (14 vs 520 bytes) — `HIDIOCGFEATURE` returns 14.

---

## Phases

### Phase 1. Solution skeleton and git ✅
- [x] `AulaManager` directory, `git init`
- [x] `PLAN.md`, `docs/PROTOCOL.md`
- [x] Solution structure: `Aula.Core`, `Aula.Cli`, `tests/*`
- [x] `.gitignore`, `Directory.Build.props` (TreatWarningsAsErrors)
- [x] First commit

### Phase 2. HID transport and device discovery ✅
- [x] `IHidTransport`, `HidSharpTransport` (HidSharp 2.6.4 API: `GetSerialNumber()`/`GetProductName()`/`GetMaxFeatureReportLength()`)
- [x] Discovery by VID `258A` / PID `010C`, vendor-interface selection, `HidDeviceScanner`
- [x] `DeviceInfo`: path, VID/PID, serial, interface; `AulaDeviceIds`
- [x] **Tests:** FakeTransport, VID/PID filter
- [ ] **Hardware acceptance:** CLI `list` finds the F75 (Windows, later Linux)

### Phase 3. Protocol layer for the F75 ✅
- [x] `F75Report`: frame-06 builder/parser (header + data, lengths, checksum)
- [x] `SinowealthProtocol`: ReadConfig/WriteConfig, color-profile frames (0x0A) and model query (0x82)
- [x] Error mapping: keyboard "acks but stays silent", timeout, wrong response length
- [x] **Tests:** "golden" frames from the reverse engineering, parsers, length validation
- [ ] **Hardware acceptance:** reading the config region from a real F75 matches the expected format

### Phase 4. Patterns, brightness, speed, color ✅
- [x] Models: `LightingConfig`, `LedEffect` (full list of F75 effects), `RgbColor`
- [x] `LightingService.Apply` — read-modify-write (3 frames for static + color profile, 2 without)
- [x] `ReadConfig`, `TurnOff`
- [x] **Tests:** serialization of `LightingConfig` into frame 0x04, effect heuristics, send count
- [x] **Hardware acceptance:** CLI `effect wave --brightness 4 --speed 3 --color ff0000` changes the backlight on a live F75 and survives a reboot (confirmed live, survives replug)

### Phase 5. Extensibility framework and F87 reference ✅
- [x] `Abstractions/`: `IAulaKeyboard`, `ILightingController`, `IKeyboardDriver`, `IKeyboardLayout`, `ITransportFactory`, `ISinowealthDiagnostics`, `KeyboardCapabilities`
- [x] `Drivers/`: `DriverRegistry` (Default = F75 + F87 via `HidSharpTransportFactory`), `SinoWealthFeatureDriver`, `AulaKeyboard`, `HidSharpTransportFactory`
- [x] `KeyboardDeviceFactory`: scanner → device selection → `Resolve` → `Open`; model override via `--model`
- [x] `ModelConfig.F87` (same VID/PID, the registry resolves F75 first — expected)
- [x] **Tests:** driver resolution, factory, VID/PID pattern matching, dispose closes the transport
- [ ] Field verification of F87 on hardware (as available)

### Phase 6. CLI on the factory ✅
- [x] All commands flow through `KeyboardDeviceFactory` (auto model detection, `--model` override)
- [x] Commands: `list`, `info`, `effects`, `effect`, `off`, `dump`, `help`
- [x] Uniform output format, exit codes, "device not found" handling
- [x] **Tests:** unit tests on arguments (Aula.Cli.Tests)
- [x] **Hardware acceptance:** the full command set (`list`, `info`, `effects`, `effect`, `off`, `dump`, `--raw-flags`, `--colorful`) ran from the console on Windows against a live F75

### Phase 7. Profiles ✅
- [x] `KeyboardProfile` (JSON): backlight, per-key colors, settings
- [x] `ProfileService`: save/load/apply
- [x] CLI: `profile save <name>`, `profile apply <name>`, `profile list`, `profile delete`
- [x] **Tests:** serialization round-trip, applying a profile = set of frames 0x04/0x06
- [ ] **Acceptance criterion:** a profile applied from the CLI is restored after a PC restart

### Phase 8. GUI (Avalonia) ✅
- [x] `Aula.App` (Avalonia 11.3.x, MVVM with CommunityToolkit.Mvvm)
- [x] Tabs: "Device", "Lighting", "Profiles"
- [x] Device: auto-detection of F75/F87, connection status, info, raw model (via `ISinowealthDiagnostics`), refresh
- [x] Lighting: effect selection from the model map, brightness 0–9, speed 0–4, RGB channels, colorful, read/apply/off
- [x] Profiles: list of saved profiles, save current, apply, delete, refresh
- [x] Shared device session (`KeyboardSession`): reopen + refresh after hotplug
- [x] **Acceptance criterion:** GUI builds and starts on Windows (real F75 — tab verification on hardware)

### Phase 9. Packaging and CI ✅
- [x] `packaging/publish.ps1`: `dotnet publish` self-contained single-file (win-x64 + x86-x64, CLI + App)
- [x] Verified on Windows: single-file `Aula.Cli.exe` (detailed sizes) and `Aula.App.exe` build and run
- [x] `packaging/linux/99-aula-keyboard.rules`: udev rule VID `258A:010C` via `TAG+="uaccess"` (no root)
- [x] `.github/workflows/ci.yml`: `windows-latest`/`ubuntu-latest` matrix — build + test + publish + artifacts (CLI/App/udev)
- [ ] **Acceptance criterion:** built binaries run on a clean machine (Windows checked partially — running locally)

### Phase 10. Field trials on F75 🔄
- [x] End-to-end scenario: read → change → reload → verify persistence (confirmed, config and color persist in flash)
- [x] Pattern map 0–255 on hardware, documented in `docs/PROTOCOL.md`
- [x] Per-key custom mode (pattern 21) — command `0x06` (planar RGB), F75Layout (88 keys), CLI pattern key=value; saved in flash (see `docs/PROTOCOL.md`)
- [ ] **Acceptance criterion:** all features of phases 4–7 are stable on hardware

### Phase 12. Auto-update ✅
- [x] Shared app version (`Directory.Build.props` `<Version>`, `ProductInfo`)
- [x] `UpdateService`: GitHub API `releases/latest` check, OS/arch asset selection (win/linux/mac), ignore prerelease
- [x] `UpdateInstaller`: download to staging + helper script (cmd/sh) — awaits process exit, replaces files, restarts
- [x] CLI: `aula update check`, `aula update install [--force]`
- [x] GUI: check on startup, status bar, update dialog with release notes and "Install & restart" button
- [x] `.github/workflows/release.yml`: triggers on `v*` tag, builds win-xed / linux-x64 / osx-arm64, zip assets, GitHub Release
- [x] **Tests:** release parsing, asset selection, prerelease, no-asset, not-found, download (Core 78, CLI 29)

### Phase 11. Other models and macOS (planned)
- SONiX-based AULA keyboards (F99/F108, etc.): a new `IKeyboardDriver` atop the same framework.
- macOS: transport porting (HID via IOKit/HidSharp is already cross-platform), Input Monitoring permissions.
- Testing on real hardware.

### Phase 13. Wireless links and Bluetooth reverse engineering 🔄
- [x] **2.4 GHz dongle supported with no code changes:** VID `258A:010C` (Compx CX-98090), the same feature-report frame 06 protocol. Verified on hardware: pattern, effect, off, dump, profile work via the dongle. In `docs/PROTOCOL.md` — currently the same.
- [x] **Bluetooth — verified as NOT supported by the field:** Classic BR/EDR (not BLE), BT HID VID `258A:010C` protocol reaches all Bluetooth; `MaxFeatureReportLength = 0` → no `SET_FEATURE`/`GET_FEATURE` → lighting protocol physically unreachable.
- [x] Practical documented: backlight only for **wired or 2.4 GHz**.
- [x] **Recovery / factory reset:** `aula reset` restores only the lighting config region
  (`00 00 01 00`); a lost keyboard input (matrix) is NOT fixed by it. The official AULA
  reset tool (`F75reset.exe`, same `HidD_SetFeature` frame protocol, not a flasher) fully
  restores the board. CLI: `aula reset --vendor <dir|exe>`. Field-verified: only the official
  tool restored typing after the lighting-only reset failed.
- [ ] **Reverse engineering BT (planned):**
  - [ ] Capture classic HID traffic (Frida / btmon / Wireshark + Bumble/HCI) on a live keyboard in BT mode
  - [ ] Explore control channel **L2CAP PSM 0x11** (unsegmented) for vendor commands
  - [ ] Probe proprietary GATT services (`0xFF*`) through a low-level BLE scanner if a BLE mode "AULA-F75 5.0 KB" exists
  - [ ] Check for a dedicated transport interface (analog of `IHidTransport`) → `SinoWealthFeatureDriver` without a feature report
  - [ ] **Acceptance criterion:** at least one command (e.g. `effect wave`) is delivered to the keyboard via BT and applied; or formally close BT as unimplementable

### Phase 14. Deep reset via vendor traffic capture (planned) 🔜
Goal: replace the `aula reset --vendor <exe>` fallback with a native `aula reset` that performs the same full restore by replicating the official `F75reset.exe` feature-report sequence. The tool uses `HidD_SetFeature` (report id 6, 520-byte frames) — the same protocol, not a flasher, so its commands are observable on the USB bus.
- [ ] **Capture:** Wireshark + **USBPcap** on the USB host controller; run the reset in `F75reset.exe` while capturing.
  - [ ] Filter selection: device VID `258A:010C`, interface with `MaxFeatureReportLength = 520`.
  - [ ] Isolate `SET_REPORT` control transfers (bRequest `0x09`, `wValue` low byte = report id `0x06` → `wValue=0x0606`), i.e. the 520-byte `06 …`outbound frames.
  - [ ] Save dump as `pcapng` in this repo (`docs/captures/f75-vendor-reset.pcapng`) for reproducibility.
- [ ] **Parser tooling:** CLI `aula capture` (or a script) that reads the `pcapng` and:
  - [ ] extracts all feature `SET_REPORT`/`GET_REPORT` frames to the AULA device,
  - [ ] maps each 520-byte frame into our `F75Report.Create*` shape (echo `CMD`, address `A0-A3`, `L0/L1`, data),
  - [ ] prints a readable command log (`06 82 model query`, `06 04 … write config`, `06 0a …`, …) to diff against `SinowealthProtocol`.
- [ ] **Map the reset sequence:** identify the exact command frameS the tool sends and what differs from our `LightingService.Reset()` (suspect: it rewrites more/full-of-config region and re-enables the keyboard matrix, not just lighting).
- [ ] **Implement native reset:** extend `LightingService.Reset()` (or a new `IKeyboardDriver`-level reset) to replay the captured sequence; verify on hardware that typing is restored without the vendor tool.
- [ ] **Tests:** "golden" captured frames as unit tests; fall back to `--vendor` when capture is unavailable.
- [ ] **Acceptance criterion:** `aula reset` (no `--vendor`) restores typing on a live F75, and the captured frames are documented in `docs/PROTOCOL.md`.
- [ ] Document findings in `docs/PROTOCOL.md` + this plan updated to ✅.