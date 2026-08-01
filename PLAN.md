# AulaManager — План реализации

Кроссплатформенное приложение на **C# / .NET** для управления настройками клавиатур AULA.

- **Целевые ОС:** Windows, Linux (первично), macOS (план на будущее).
- **Первая модель:** AULA F75 (проводная USB), чипсет SinoWealth / VID `258A:010C`.
- **Референс-модель:** AULA F87 (тот же чип SinoWealth, тот же VID/PID) — подтверждает расширяемость каркаса.
- **Первая фича:** настройка подсветки клавиш и эффектов.

## Стек

| Слой | Выбор |
|---|---|
| Язык / рантайм | C# 12, .NET 8 LTS |
| HID | HidSharp 2.6.x (Windows HidP, Linux hidraw, macOS IOKit) |
| Профили | System.Text.Json |
| Тесты | xUnit |
| CLI | Aula.Cli (console, тот же core) |
| GUI (этап 8+) | Avalonia UI |

## Структура решения

```
AulaManager.slnx
├── src/
│   ├── Aula.Core/              # домен, протокол, драйверы, сервисы (net8.0, без UI)
│   │   ├── Abstractions/       # контракты расширяемости (IAulaKeyboard, IKeyboardDriver, …)
│   │   ├── Devices/            # сканер, DeviceInfo, AulaDeviceIds
│   │   ├── Drivers/            # DriverRegistry, SinoWealthFeatureDriver, AulaKeyboard, транспорт
│   │   ├── Models/             # ModelConfig (F75/F87), KeyboardConfig, LedEffect, RgbColor
│   │   ├── Protocol/           # кадры 06, HidSharpTransport, SinowealthProtocol
│   │   └── Services/           # KeyboardDeviceFactory, LightingService
│   ├── Aula.Cli/               # консольный фронт
│   └── Aula.App/               # Avalonia UI (позже)
├── tests/
│   ├── Aula.Core.Tests/        # xUnit (+ TestHelpers: FakeTransport, FakeScanner, FakeTransportFactory)
│   └── Aula.Cli.Tests/         # парсер команд
├── packaging/
│   └── linux/99-aula-keyboard.rules
├── docs/
│   └── PROTOCOL.md             # документирование протокола F75
└── PLAN.md
```

## Принципы

1. Каждый этап завершается: реализация → тесты → сборка/прогон → коммит в git.
2. Ядро (`Aula.Core`) не знает про UI — CLI и GUI используют одни сервисы.
3. OS-специфика изолирована за интерфейсами (`IHidTransport`).
4. Без реальной клавиатуры логика покрывается юнит-тестами на «золотые» кадры.
5. **Расширяемость:** подключить новую модель = `ModelConfig` + регистрация драйвера (SinoWealth-чип), либо новый `IKeyboardDriver` (другой чип — SONiX и т.п.). Приложение работает только через `IAulaKeyboard`/`ILightingController`.

## Известное о протоколе F75/F87 (подтверждено реверсом F87)

- HID **Feature Report, Report ID 6, 520 байт**, vendor-интерфейс (usage_page `0xFF00`/`0xFF13`).
- Кадр: `06 CMD A0 A1 A2 A3 L0 L1 <data…>`
  - `0x04` — запись конфигурации, `0x84` — чтение
  - `0x0A` — запись color profile (per-key цвета), `0x82` — запрос модели
  - config-область: адрес `00 00 01 00`, длина `0x0080`
  - color profile: RGB первой клавиши на байтах 29–31, терминатор `5A A5` на `0x202/0x203`
  - ответ на `0x82` — 14 байт: `06 82 01 00 01 00 06 00 03 00 00 00 03 66`
- Чтение: сначала `SET_FEATURE` (запрос), затем `GET_FEATURE` (6). Ответ на `0x84` — 136 байт (8 заголовок + 128 payload).
- Ответ конфига: эффект — offset 18, custom mode — 17, side light — 26, battery — 36; параметры эффекта на `64 + 2×effect_id` (яркость, `speed<<4 | flags`).
- Таблица эффектов: **яркость 0–9** (фабричный конфиг `0x09`), скорость 0–4 — обход через host-рендеринг в будущем.
- Записи в config-область **сохраняются в flash клавиатуры сразу** (переживают перезагрузку).
- **Карта эффектов на железе:** реактивные — 4 (spectrum), 7 (starlight), 12 (laser); только single-color — 14 (gradient); не существуют — 19, 20; custom 21 требует per-key таблицу (не реализовано). Подробности в `docs/PROTOCOL.md`.
- **Открытые вопросы:** команда «commit/latch» для отображения per-key цветов; длина ответа GET_FEATURE на Linux (14 vs 520 байт) — `HIDIOCGFEATURE` возвращает 14.

---

## Этапы

### Этап 1. Скелет решения и git ✅
- [x] Каталог `AulaManager`, `git init`
- [x] `PLAN.md`, `docs/PROTOCOL.md`
- [x] Структура решения: `Aula.Core`, `Aula.Cli`, `tests/*`
- [x] `.gitignore`, `Directory.Build.props` (TreatWarningsAsErrors)
- [x] Первый коммит

### Этап 2. Транспорт HID и обнаружение устройства ✅
- [x] `IHidTransport`, `HidSharpTransport` (HidSharp 2.6.4 API: `GetSerialNumber()`/`GetProductName()`/`GetMaxFeatureReportLength()`)
- [x] Обнаружение по VID `258A` / PID `010C`, выбор vendor-интерфейса, `HidDeviceScanner`
- [x] `DeviceInfo`: путь, VID/PID, серийник, интерфейс; `AulaDeviceIds`
- [x] **Тесты:** FakeTransport, фильтр по VID/PID
- [ ] **Приёмка на железе:** CLI `list` находит F75 (Windows, позже Linux)

### Этап 3. Протокольный слой F75 ✅
- [x] `F75Report`: билдер/парсер кадра 06 (заголовок + data, длины, контроль суммы)
- [x] `SinowealthProtocol`: ReadConfig/WriteConfig, кадры color profile (0x0A) и model query (0x82)
- [x] Маппинг ошибок: клавиатура «ACK и молчит», timeout, неверная длина ответа
- [x] **Тесты:** «золотые» кадры из реверса, парсеры, валидация длины
- [ ] **Приёмка на железе:** чтение config-области с реальной F75 совпадает с ожидаемым форматом

### Этап 4. Подсветка: эффекты, яркость, скорость, цвет ✅
- [x] Модели: `LightingConfig`, `LedEffect` (полный список эффектов F75), `RgbColor`
- [x] `LightingService.Apply` — read-modify-write (3 кадра при static + color profile, 2 — без)
- [x] `ReadConfig`, `TurnOff`
- [x] **Тесты:** сериализация `LightingConfig` в кадр 0x04, эвристика эффектов, число отправок
- [x] **Приёмка на железе:** CLI `effect wave --brightness 4 --speed 2 --color ff0000` меняет подсветку на живой F75 и переживает перезагрузку (подтверждено камерой + сохранение после replug)

### Этап 5. Каркас расширяемости и референс F87 ✅
- [x] `Abstractions/`: `IAulaKeyboard`, `ILightingController`, `IKeyboardDriver`, `IKeyboardLayout`, `ITransportFactory`, `ISinowealthDiagnostics`, `KeyboardCapabilities`
- [x] `Drivers/`: `DriverRegistry` (Default = F75 + F87 через `HidSharpTransportFactory`), `SinoWealthFeatureDriver`, `AulaKeyboard`, `HidSharpTransportFactory`
- [x] `KeyboardDeviceFactory`: сканирование → выбор устройства → `Resolve` → `Open`; override модели через `--model`
- [x] `ModelConfig.F87` (тот же VID/PID, реестр резолвит F75 первым — ожидаемо)
- [x] **Тесты:** разрешение драйверов, фабрика, матчинг VID/PID, dispose закрывает транспорт
- [ ] Полевая проверка F87 на железе (по мере наличия)

### Этап 6. CLI на фабрике ✅
- [x] Все команды через `KeyboardDeviceFactory` (автоопределение модели, `--model` — override)
- [x] Команды: `list`, `info`, `effects`, `effect`, `off`, `dump`, `help`
- [x] Единый формат вывода, exit codes, обработка «устройство не найдено»
- [x] **Тесты:** unit-тесты на аргументы (Aula.Cli.Tests)
- [x] **Приёмка на железе:** весь набор команд (`list`, `info`, `effects`, `effect`, `off`, `dump`, `--raw-flags`, `--colorful`) отработал из консоли на Windows с живой F75

### Этап 7. Профили ✅
- [x] `KeyboardProfile` (JSON): подсветка, цвета per-key, настройки.
- [x] `ProfileService`: save/load/apply.
- [x] CLI: `profile save <name>`, `profile apply <name>`, `profile list`, `profile delete`.
- [x] **Тесты:** round-trip сериализация, применение профиля = набор кадров 0x04/0x06.
- [ ] **Критерий приёмки:** профиль, применённый с CLI, восстанавливается после выключения ПК.

### Этап 8. GUI (Avalonia)
- Вкладки: «Устройство», «Подсветка», «Профили».
- Превью раскладки 75%, выбор эффекта, цвета, яркости, скорости (UI строится из `KeyboardCapabilities`).
- Hotplug-индикация.
- **Критерий приёмки:** GUI собирается и работает на Windows и Linux.

### Этап 9. Упаковка и CI
- GitHub Actions matrix: `windows-latest`, `ubuntu-latest`.
- `dotnet publish` self-contained single-file под win-x64 и linux-x64.
- udev-правило для Linux (установка через `uaccess`, без постоянного root).
- **Критерий приёмки:** собранные бинарники запускаются на чистой машине.

### Этап 10. Полевые испытания на F75 🔄
- [x] Сквозной сценарий: прочитать → изменить → перезагрузить → проверить сохранение (подтверждено, конфиг и цвет сохраняются в flash)
- [x] Карта эффектов 0–21 на железе (камера + нажатия), задокументирована в `docs/PROTOCOL.md`
- [x] Per-key custom mode (эффект 21) — команда `0x06` (planar RGB), F75Layout (88 клавиш), CLI `perkey key=color`; сохраняется в flash (см. `docs/PROTOCOL.md`)
- [ ] **Критерий приёмки:** все фичи этапов 4–7 стабильны на железе

### Этап 11. Другие модели и macOS (план)
- Сонэкс-клавиатуры AULA (F99/F108 и др.): новый `IKeyboardDriver` поверх того же каркаса.
- macOS: перенос транспорта (HID через IOKit/HidSharp уже кросс-платформенен), права Input Monitoring.
- Тестирование на реальном железе.
