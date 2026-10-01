# Промпт 08a — Ядро сейва и настроек (без подключения к геймплею)

**Ветка:** `feature/08a-save-core` (от `main`) · **Рабочая папка:** отдельный git worktree `…/GitHub/MidnightWorksTestTask-wt-save`
**Перед началом:** `CLAUDE.md`, `Docs/TDD.md` §3, §4.15, §4.16 (настройки), §6, `Docs/ARCHITECTURE.md`.

> Параллельно в основной папке идёт A1 (`feature/04-build`). Поэтому **только новые файлы в новых папках**. Существующие файлы трогать **только** из списка §5. Не трогать `GameplayEntryPoint`, `LocationTraffic`, точки, конфиги, сцены и Editor-утилиты.

---

## 1. Цель
Инфраструктура сейва и настроек, к которой модули B/C потом подключат свои данные:
- DTO сейва с версией;
- атомарная запись на диск;
- реестр `ISaveable` и автосейв;
- сервис настроек на `PlayerPrefs`.

Сам геймплей (кошелёк, постройки, персонал) в этом модуле **не** подключается.

## 2. Services (`noEngineReferences`)

### 2.1 `Services/Save/`
- **`SaveData`** — `[System.Serializable] sealed class` с **public-полями**. `// Why:` JsonUtility сериализует только public / `[SerializeField]` поля, а `UnityEngine` здесь недоступен. Это единственное исключение из правила «нет public-полей», объяснить в XML-доке.
  - Поля: `int version`, `long savedAtUtcTicks`, `long money`, `int xp`, `int level`, `string[] builtPlotIds`, `PointSaveData[] points`, `string[] storekeeperLocationIds`, `string[] unlockedLocationIds`, `float vipLoyalty`, `int tutorialStep`.
  - `const int CurrentVersion = 1`.
  - `static SaveData CreateEmpty()` — массивы пустые, не null.
- **`PointSaveData`** (`[Serializable]`, public-поля): `string pointId`, `int speedLevel`, `int priceLevel`, `int supply` (−1 = не сохранено), `bool hasWorker`.
- **`ISaveStorage`** (порт к диску; реализация в Infrastructure):
  `bool TryRead(out string json)`, `void Write(string json)`, `void Delete()`, `bool Exists { get; }`.
- **`ISaveSerializer`**: `string Serialize(SaveData)`, `bool TryDeserialize(string json, out SaveData data)`. **Why:** JsonUtility живёт в UnityEngine, поэтому Services видит только интерфейс.
- **`ISaveable`**:
  ```csharp
  void Capture(SaveData data);   // writes own slice
  void Restore(SaveData data);   // reads own slice; must tolerate missing/empty arrays
  ```
- **`ISaveService`**:
  - `bool HasSave`, `bool TryLoad(out SaveData data)`, `void Save(SaveData data)`, `void Delete()`.
  - Реализация `SaveService` (ctor: `ISaveStorage`, `ISaveSerializer`, `ITimeProvider`, `IGameLogger`).
  - `Save` проставляет `version` и `savedAtUtcTicks`.
  - `TryLoad` возвращает false и пишет предупреждение, если:
    - JSON битый (`SaveService` не удаляет файл: решение принимает вызывающий);
    - `version > CurrentVersion` (сейв из будущей версии);
    - `version < 1`.
  - После загрузки нормализует null-массивы в пустые.
  - Миграции: `private SaveData Migrate(SaveData)`, сейчас без шагов. Оставить точку расширения и `// Why:`.
- **`SaveCoordinator : ITickable, IDisposable`** (ctor: `ISaveService`, `IGameLogger`, `float autosaveIntervalSeconds = 30f`):
  - `void Add(ISaveable)` — дубликат по ссылке игнорируется. `Remove(ISaveable)`.
  - `bool TryRestore()` — `TryLoad` → `Restore` у всех в порядке добавления. Возвращает, был ли сейв.
  - `void SaveNow()` — `CreateEmpty` → `Capture` у всех по порядку → `Save`. Исключение в одном saveable логируется, остальные продолжают. Сейв всё равно пишется, а в лог уходит id/тип упавшего.
  - `void RequestSave()` — отложенный сейв: выполняется в следующем `Tick` один раз, даже если запрошен много раз за кадр. Нужен для «после постройки/найма».
  - `Tick(dt)`: накапливает время (на паузе dt = 0, автосейва нет), раз в интервал → `SaveNow`. Плюс отложенный запрос. Ноль аллокаций в тике, если сохранять нечего.
  - `void ResetProgress()` — `Delete` + флаг «не сохранять до следующего `TryRestore`/`SaveNow`», чтобы автосейв не записал старое состояние обратно перед перезагрузкой сцены.
  - Публикация в шину не нужна.

### 2.2 `Services/Settings/`
- **`GameSettings`** (неизменяемый класс): `float MusicVolume` (0..1), `float SfxVolume` (0..1), `int QualityLevel`, `bool Fullscreen`, `int ResolutionWidth`, `int ResolutionHeight` (0 = текущее). `With…`-методы возвращают копию. Валидация с clamp.
- **`ISettingsStore`** (порт): `bool TryLoad(out GameSettings)`, `void Save(GameSettings)`.
- **`ISettingsApplier`** (порт): `void Apply(GameSettings settings)`.
- **`ISettingsService`**: `GameSettings Current`, `void Set(GameSettings settings)` (сохранить + применить + событие), `event Action<GameSettings> Changed`.
  - Реализация `SettingsService` (ctor: `ISettingsStore`, `ISettingsApplier`, `GameSettings defaults`).
  - В ctor грузит сохранённые настройки или берёт дефолты. `IInitializable.Initialize()` применяет загруженные.

## 3. Infrastructure

### `Infrastructure/Save/`
- **`JsonUtilitySaveSerializer : ISaveSerializer`**:
  - `JsonUtility.ToJson(data, prettyPrint: false)`;
  - `TryDeserialize` ловит `ArgumentException` и пустую строку.
- **`FileSaveStorage : ISaveStorage`** (ctor: `string directory`, `string fileName = "save.json"`, `IGameLogger`):
  - Атомарная запись: `save.json.tmp` → если основной файл есть, `File.Replace(tmp, main, backup: main + ".bak")`, иначе `File.Move`.
  - `// Why:` падение посреди записи не должно оставить битый сейв.
  - `TryRead`: если основного нет, но есть `.bak`, читать бэкап.
  - IO-исключения ловить и логировать, наружу не пробрасывать. `Write` при ошибке логирует и оставляет старый файл.
  - Путь передаёт EntryPoint: `Application.persistentDataPath`. Сам класс `Application` не трогает (так его можно тестировать на временной папке).
  - `// Why:` на кириллице в пути: `persistentDataPath` у Windows-пользователя может содержать не-ASCII, поэтому используем `System.IO` с полными путями, без ручной склейки через `/`.

### `Infrastructure/Settings/`
- **`PlayerPrefsSettingsStore : ISettingsStore`** — ключи `settings.music`, `settings.sfx`, `settings.quality`, `settings.fullscreen`, `settings.resW`, `settings.resH`. `PlayerPrefs.Save()` после записи.
- **`UnitySettingsApplier : ISettingsApplier`**:
  - `QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true)` с clamp по `QualitySettings.names.Length`;
  - `Screen.SetResolution(w, h, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed)`, если w/h > 0, иначе только `Screen.fullScreenMode`;
  - громкость **пока не применяется** (AudioMixer — модуль 12): `// TODO(12-visual): route volumes to the AudioMixer.`

## 4. Bootstrap — `ProjectEntryPoint` (единственный изменяемый файл кода)
В `Awake` после конфигов:
- `ISaveService` — `new SaveService(new FileSaveStorage(Application.persistentDataPath, "save.json", _logger), new JsonUtilitySaveSerializer(), timeProvider, _logger)`;
- `ISettingsService` — `new SettingsService(new PlayerPrefsSettingsStore(), new UnitySettingsApplier(), defaults)` и сразу `Initialize()`. Дефолты: музыка 0.7, SFX 0.8, quality = `QualitySettings.GetQualityLevel()`, fullscreen = `Screen.fullScreen`, разрешение 0×0.

`SaveCoordinator` в этом модуле **не** создаётся: он сценовый, его соберёт модуль 08b в Gameplay-инсталлере.

## 5. Существующие файлы, которые можно менять
- `Assets/_Project/Scripts/Bootstrap/ProjectEntryPoint.cs` — §4.
- `Assets/_Project/Tests/EditMode/AutoService.Tests.EditMode.asmdef` — добавить ссылку `AutoService.Infrastructure` (для тестов файлового хранилища и сериализатора).

Больше ничего.

## 6. Тесты (`Tests/EditMode/`, новые файлы)
- **`SaveServiceTests`** (фейковые storage и serializer или настоящий `JsonUtilitySaveSerializer`):
  - round-trip всех полей, включая массивы `PointSaveData`;
  - битый JSON → false;
  - `version` из будущего → false;
  - null-массивы нормализуются;
  - `Save` ставит version и время из фейкового `ITimeProvider`.
- **`SaveCoordinatorTests`** (фейковые `ISaveService` и `ISaveable`):
  - `SaveNow` вызывает `Capture` по порядку;
  - `TryRestore` без сейва → false, `Restore` не вызывается;
  - автосейв ровно через 30 с, при dt = 0 нет;
  - 3 × `RequestSave` за кадр → один сейв в следующем тике;
  - исключение в одном saveable не мешает остальным;
  - после `ResetProgress` автосейв не пишет.
- **`FileSaveStorageTests`** (временная папка через `System.IO.Path.GetTempPath()` + Guid, удалить в `TearDown`):
  - запись/чтение;
  - повторная запись создаёт `.bak`;
  - чтение из `.bak`, если основного нет;
  - `Delete`.
- **`SettingsServiceTests`** (фейковые store и applier):
  - дефолты, если сохранённого нет;
  - `Set` сохраняет, применяет и шлёт `Changed`;
  - clamp громкости.

Фейки — отдельными файлами (`FakeSaveService`, `FakeSaveable`, `FakeTimeProvider`, если такого нет), по одному типу на файл.

## 7. Правила
- Все правила `CLAUDE.md`: XML-доки на public API, `// Why:`, `sealed`, `_camelCase`, английский, никаких `UnityEngine` в Services, `.meta` не создавать.
- **Unity в этой папке не открыт**, тесты прогнать нельзя. Поэтому компилируемость проверяй глазами особенно тщательно:
  - `using`'и;
  - namespace = `AutoService.<Слой>.<Папка>` (`AutoService.Services.Save`, `AutoService.Services.Settings`, `AutoService.Infrastructure.Save`, `AutoService.Infrastructure.Settings`);
  - доступные API Unity 6;
  - перегрузки `File.Replace` / `File.Move`.
- Не трогать основную папку проекта `…/GitHub/MidnightWorksTestTask` ни в коем случае. Все команды — с путями worktree / `git -C <worktree>`.

## 8. Git
Коммиты в `feature/08a-save-core` (в worktree), **не пушить**:
1. `Add save data, save service and coordinator`
2. `Add settings service`
3. `Add file save storage, JsonUtility serializer and PlayerPrefs settings`
4. `Register save and settings in ProjectEntryPoint`
5. `Add save and settings tests`

Этот файл промпта закоммитить первым коммитом: `Docs: 08a prompt`.

В конце — отчёт: список файлов, решения, отклонения от промпта, что проверить при первом открытии в Unity. Editor setup в этом модуле не нужен: новые скрипты не вешаются на объекты.
