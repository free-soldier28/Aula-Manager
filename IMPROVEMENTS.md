# AulaManager — Improvements Roadmap

Itemized list of improvements, ordered by priority. Check each item off as it is
implemented (implementation → tests → build/run).

## Bugs / data loss

### 1. Profiles don't save color or per-key map
- [ ] `KeyboardProfile.FromCurrent` (`src/Aula.Core/Models/KeyboardProfile.cs`) stores
      only effect/brightness/speed/colorful — `Color` and `KeyColors` are not read.
- [ ] Applying a static "red" profile won't restore red.
- [ ] Fix: read `ReadColorProfileRaw()` and per-key colors in `FromCurrent`.

### 2. Hardcoded LED count in ProfileService
- [ ] `src/Aula.Core/Services/ProfileService.cs` uses `new RgbColor[126]`
      instead of `keyboard.Layout.LedCount` — breaks on other layouts.

### 3. GUI can't save per-key colors to a profile
- [ ] `ProfilesViewModel.Save` (`src/Aula.App/ViewModels/ProfilesViewModel.cs`) doesn't
      read the colors from `PerKeyViewModel`; only a plain lighting profile is saved.

## GUI (Avalonia)

### 4. No unit tests for ViewModels
- [ ] All UI (Lighting/PerKey/Profiles/Update) is untested; ViewModels call
      `KeyboardSession`/`new ProfileService()` directly, coupling UI to hardware.
- [ ] Introduce fakes for `KeyboardSession` and services; add tests.

### 5. Synchronous `_session.Open()` in MainWindowViewModel constructor
- [ ] `src/Aula.App/ViewModels/MainWindowViewModel.cs` scans HID on the UI thread at
      startup; may stall on Linux. Move to background.

### 6. Auto-reconnect on hotplug
- [x] USB change watcher (`IHidDeviceListWatcher` / `HidSharpDeviceListWatcher`) in Core.
- [x] `ReconnectPlanner` decision logic (reconnect when device returns, drop when gone).
- [x] `KeyboardSession` subscribes to the watcher and re-opens/releases automatically.
- [ ] Hardware acceptance: unplug + replug the F75 while the GUI is running.

### 7. Duplicated color-wheel code
- [ ] `LightingViewModel` and `PerKeyViewModel` duplicate Red/Green/Blue +
      `_syncingWheel` + palette logic. Extract a shared base class.

### 8. No per-key read-back in GUI
- [ ] `PerKeyView` doesn't load current colors from the keyboard on open (CLI `dump`
      has it, GUI doesn't).

### 9. Update install re-checks network
- [ ] `UpdateViewModel.InstallAsync` calls `CheckAsync` again instead of reusing the
      already-fetched `UpdateInfo` — extra network call + race risk.

## Packaging / distribution

### 10. No installer
- [ ] Windows: MSI / Inno Setup + code signing + app icon (`.ico` in App csproj).
- [ ] Linux: `.deb` / AppImage. Currently only a zip of the exe.

### 11. Duplicated ParseLevel
- [ ] `ParseLevel` is duplicated in CLI `Program.cs` and App `Program.cs`. Move to Core.

### 12. Log file grows unbounded
- [ ] App `Program.cs` appends to `aula-<date>.log` forever; add rotation / size cap.

### 13. Update zip not verified
- [ ] Downloaded update archive isn't checked against a hash/signature from the release.

## Plan / domain

### 14. Remaining PLAN.md items
- [ ] Phase 14: deep reset via vendor traffic capture (`aula reset` without `--vendor`
      should fully restore the board, incl. typing).
- [ ] Bluetooth reverse engineering, SONiX keyboards, macOS port.

### 15. ModelConfig.Resolve silently falls back to F75
- [ ] `src/Aula.Core/Models/ModelConfig.cs` silently falls back to F75 for unknown ids.
      Better: warn or throw.
