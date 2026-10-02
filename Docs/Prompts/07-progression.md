# Модуль 07 (B1) — Прогрессия: XP, уровни, разблокировки

**Пишет:** пользователь (архитектор ревьюит, как код кодера).
**Ветка:** `feature/07-progression` в отдельной папке `…/GitHub/MidnightWorksTestTask-wt-progression` (git worktree). Unity на ней не открывать. Пиши в Rider/VS, тесты прогонит архитектор после переноса ветки в основную папку.
**Перед началом:** `CLAUDE.md` (правила), `Docs/GDD.md` §9, `Docs/ARCHITECTURE.md` §7–8.

Образцы стиля:
- `Domain/Economy/Wallet.cs` — сущность с событием;
- `Services/Economy/WalletService.cs` — сервис-обёртка + шина;
- `Services/Building/BuildService.cs` — работа с `IUnlockGate`;
- `Tests/EditMode/WalletServiceTests.cs`, `BuildServiceTests.cs` — тесты и фейки.

---

## 1. Что получится
- За каждую обслуженную машину игрок получает XP. Количество зависит от типа услуги: парковка 1, мойка 2, масло 3, шины 4 (тюнинг 6, покраска 8 — для локации 2).
- XP копится, пороги уровней: **1 → 0, 2 → 15, 3 → 35, 4 → 70, 5 → 110**, дальше **+50 за уровень**.
- Новый уровень открывает постройки, найм и апгрейды, у которых `RequiredLevel` ≤ уровня. Сейчас всё открыто заглушкой `AlwaysUnlockedGate` — её заменяет твой `LevelUnlockGate`.
- XP-бар и попап «New level!» делает кодер в B2 по твоему интерфейсу. Сохранение XP — тоже B2 (вызовет твой `Restore`).

## 2. Domain — `Assets/_Project/Scripts/Domain/Progression/` (namespace `AutoService.Domain.Progression`)

### `LevelTable` (неизменяемый, `sealed`)
```csharp
public LevelTable(IReadOnlyList<int> thresholds, int xpPerLevelAfterTable)
// thresholds[0] == 0, строго по возрастанию; xpPerLevelAfterTable > 0 — иначе ArgumentException
public int LevelForXp(int xp)          // xp < 0 → ArgumentOutOfRangeException; 0 → 1; 15 → 2; 110 → 5; 160 → 6; 210 → 7
public int XpForLevel(int level)       // сколько XP нужно, чтобы БЫТЬ на уровне: 1 → 0, 2 → 15, 6 → 160
```
Подсказка: уровни 1-based, а индекс массива 0-based → `thresholds[level - 1]`. За концом таблицы: `last + (level - count) * xpPerLevelAfterTable`. Цикл с `for`, без LINQ.

### `PlayerProgress` (сущность, `sealed`)
```csharp
public PlayerProgress(LevelTable table)        // Xp = 0, Level = 1
public int Xp { get; }
public int Level { get; }
public event Action<PlayerProgress> Changed;            // после любого изменения XP
public event Action<PlayerProgress, int> LeveledUp;     // новый уровень; при прыжке на 2 уровня — дважды, по порядку
public void AddXp(int amount)                  // amount <= 0 → ArgumentOutOfRangeException
public void Restore(int xp)                    // для сейва: ставит XP и уровень, шлёт Changed, LeveledUp НЕ шлёт
public float LevelProgress01 { get; }          // доля пути от начала текущего уровня до следующего (для XP-бара)
public int XpToNextLevel { get; }              // сколько осталось до следующего уровня
```
Подсказки:
- `LeveledUp` шли в цикле: «пока уровень по таблице больше текущего → Level++ → событие». Тогда прыжок через уровень даёт два попапа.
- `// Why:` у `Restore`: почему без `LeveledUp` — загрузка сейва не должна показывать попапы и давать награды.

## 3. Services — `Assets/_Project/Scripts/Services/Progression/` (namespace `AutoService.Services.Progression`)

### `IProgressionService`
```csharp
int Xp { get; }  int Level { get; }  float LevelProgress01 { get; }  int XpToNextLevel { get; }
event Action Changed;
event Action<int> LeveledUp;                   // новый уровень
void Restore(int xp);                          // для сейва (B2)
```

### `ProgressionService : IProgressionService, IDisposable`
- ctor: `PlayerProgress progress, IConfigProvider config, IEventBus eventBus`.
- Подписывается на `ServiceCompletedEvent` (namespace `AutoService.Services.Points`; приходит и для шлагбаума — это XP за парковку).
- XP = `config.TryGetServiceType(evt.ServiceTypeId, out var type) ? type.XpReward : 0`. 0 → ничего не делать.
- На `PlayerProgress.LeveledUp` публикует в шину `LevelUpEvent { int NewLevel }`.
  - Новый `readonly struct` в этой же папке, по образцу `BuildCompletedEvent`.
  - Нужен онбордингу и звуку.
- `Dispose` отписывается от всего.
- Обработчик события — метод, а не лямбда. Иначе не отписаться, и это лишняя аллокация.

### `LevelUnlockGate : IUnlockGate, IDisposable`
`IUnlockGate` уже есть в `Services/Building/IUnlockGate.cs`.
- ctor: `IProgressionService progression`.
- `IsUnlocked(int requiredLevel) => progression.Level >= requiredLevel`.
- `Changed` поднимать на `progression.LeveledUp`.
- `Dispose` отписывается.

## 4. Конфиг

### XP за услугу
- `Infrastructure/Config/ServiceTypeConfig.cs`: `[SerializeField, Min(0)] private int _xpReward;` + свойство.
- `Services/Config/ServiceTypeSettings.cs`: параметр ctor `int xpReward` (проверка `>= 0`) + свойство `XpReward`. Параметр добавь **последним**.
- В `ScriptableObjectConfigProvider.MapServiceTypes` передай значение.
- Найди все `new ServiceTypeSettings(` поиском, в тестах и фейках тоже, и допиши аргумент.

### Таблица уровней
- `Infrastructure/Config/ProgressionSection.cs` — `[Serializable]`-класс, по образцу `TrafficSection`: `private int[] _levelThresholds = { 0, 15, 35, 70, 110 };`, `private int _xpPerLevelAfterTable = 50;`.
- В `GameConfig` поле `_progression`.
- `Services/Config/ProgressionSettings.cs` — неизменяемая, создаёт `LevelTable`.
- `IConfigProvider.Progression` + маппинг в провайдере. В `Tests/EditMode/FakeConfigProvider.cs` — свойство.

## 5. Подключение — `Bootstrap/Installers/`
- Новый `ProgressionInstaller : IGameplayInstaller`, образец — `EconomyInstaller`. Создаёт `PlayerProgress` + `ProgressionService` и регистрирует `IProgressionService`.
- В `GameplayEntryPoint` вызвать его **до** `BuildingInstaller`.
- `BuildingInstaller.cs`, строка с `TODO(07-progression)`: вместо `new AlwaysUnlockedGate()` → `new LevelUnlockGate(context.Resolve<IProgressionService>())`. Зарегистрировать так, чтобы он диспоузился (`context.Register` / `Track` — посмотри, как там регистрируется gate сейчас).
- `AlwaysUnlockedGate` не удалять, он пригодится тестам.

## 6. Тесты — `Tests/EditMode/`
- `LevelTableTests`:
  - границы 0/14/15/109/110/160/210;
  - `XpForLevel` 1/2/5/6/7;
  - плохие таблицы: пустая, не с 0, не по возрастанию, шаг 0 → исключение.
- `PlayerProgressTests`:
  - +15 → уровень 2, одно `LeveledUp(2)`;
  - +40 с нуля → два события 2 и 3, по порядку;
  - `Restore(70)` → уровень 4, `LeveledUp` не было, `Changed` было;
  - `LevelProgress01` на середине уровня ≈ 0.5;
  - `AddXp(0)` → исключение.
- `ProgressionServiceTests`:
  - настоящий `EventBus` + фейковый конфиг;
  - `ServiceCompletedEvent` мойки → +2 XP;
  - неизвестный тип → 0;
  - переход уровня → `LevelUpEvent` в шине;
  - после `Dispose` события не считаются.
- `LevelUnlockGateTests`: уровень 1 → `IsUnlocked(2) == false`; после уровня 2 → true и `Changed` вызван.

## 7. Правила (из CLAUDE.md, коротко)
- Domain и Services без `UnityEngine`.
- XML-доки `///` на всё public, `// Why:` на неочевидное.
- Английский в коде, `sealed`, `_camelCase`, один тип — один файл.
- Каждая подписка с отпиской, никаких лямбд-подписок.
- `.meta` не создавать: их создаст Unity.

## 8. Баланс — после включения настоящего gate
Требования уровней уже прописаны в конфигах:
- стройка: Wash 2 — ур. 2, Oil — 3, Tires — 4, места P3 / P4 — 2 / 3;
- найм: парковщик 1, мойщик 2, масло 3, шины 4;
- кладовщик 3.

После мержа это начнёт работать по-настоящему. Проверь в Play, что на старте можно нанять парковщика, а мойку 2 — только с уровня 2.

**Editor (после переноса ветки):** в инспекторе `ST_Parking` / `ST_Wash` / `ST_Oil` / `ST_Tires` поставить XP Reward 1 / 2 / 3 / 4. Таблица уровней в `GameConfig` подставится из значений по умолчанию — проверь.

## 9. Git
Коммить маленькими шагами в `feature/07-progression`:
1. `Add level table and player progress`
2. `Add progression service and level unlock gate`
3. `Add progression config`
4. `Wire progression and level gate`
5. `Add progression tests`

Потом `git push -u origin feature/07-progression` и напиши архитектору «B1 готов».
