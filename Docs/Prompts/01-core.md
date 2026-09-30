# Промпт 01 — Структура, asmdef, Core, Economy, Config-база

**Ветка:** `feature/01-core` (от `main`) · **PR:** в `main`

---

## 1. Контекст

Ты — Unity-разработчик (кодер) в проекте **Auto Service Tycoon** — 3D Idle Tycoon про автосервис, Unity **6000.3.17f1**, URP, PC. Это тестовое задание; критерии оценки включают **чистый, структурированный, комментированный код** и **масштабируемость**.

Архитектура — Clean Architecture / DDD, слои разделены через Assembly Definitions:

```
Presentation ──► Services ──► Domain
Infrastructure ──► Services, Domain   (реализует интерфейсы из Services)
Bootstrap ──► все (только сборка графа зависимостей)
```

Этот промпт закладывает **фундамент**: структуру папок, сборки, Composition Root, игровой цикл, шину событий, базовые абстракции окружения, экономику (деньги + кошелёк) и базу конфигов. Геймплея ещё нет. Результат — пустая игра, которая корректно стартует через Boot-сцену, собирает граф сервисов и пишет в консоль стартовый баланс.

## 2. Жёсткие правила (для всего проекта)

1. **Никаких сторонних библиотек** (Zenject, UniTask, DOTween, Odin и т.п.). Только `com.unity.*` и чистый C#.
2. `Domain` и `Services` собираются с `noEngineReferences: true` — **ни одного `using UnityEngine`** там. Логирование — через `IGameLogger`.
3. **Никаких синглтонов, статического изменяемого состояния, `FindObjectOfType`, `GameObject.Find`.** Все зависимости — через конструктор (C#-классы) или метод `Construct(...)`/`Enter(...)` (MonoBehaviour). Исключение: Editor-утилиты в `*.Editor` сборке.
4. **Ноль аллокаций в `Tick`**: без LINQ, лямбд с замыканиями, boxing, конкатенации строк, `GetComponent`.
5. Каждая подписка на событие имеет отписку.
6. **Язык кода — английский:** идентификаторы и комментарии.
7. **XML-документация (`/// <summary>`) на каждом public/protected типе и члене.** Неочевидные решения — комментарий `// Why: ...`.
8. Стиль: приватные поля `_camelCase`; `[SerializeField] private` вместо public-полей; один тип — один файл, имя файла = имя типа; классы `sealed`, если не задуманы для наследования; явные модификаторы доступа.
9. Нет `Update()` в геймплейных классах — всё тикает через `GameLoop`.

## 3. Структура папок и сборок

Создай:

```
Assets/_Project/
  Scripts/
    Domain/          AutoService.Domain.asmdef
    Services/        AutoService.Services.asmdef
    Infrastructure/  AutoService.Infrastructure.asmdef
    Presentation/    AutoService.Presentation.asmdef   (пока без скриптов)
    Bootstrap/       AutoService.Bootstrap.asmdef
      Editor/        AutoService.Bootstrap.Editor.asmdef
  Tests/
    EditMode/        AutoService.Tests.EditMode.asmdef
  Configs/
  Scenes/
  Prefabs/
  Art/
  UI/
  Audio/
  Materials/
```

Пустые папки git не хранит — положи в каждую пустую `.gitkeep`.

### asmdef (ссылки по имени, `rootNamespace` = имя сборки)

| Сборка | references | Особенности |
|---|---|---|
| `AutoService.Domain` | — | `noEngineReferences: true` |
| `AutoService.Services` | Domain | `noEngineReferences: true` |
| `AutoService.Infrastructure` | Domain, Services | |
| `AutoService.Presentation` | Domain, Services, `Unity.InputSystem`, `Unity.AI.Navigation`, `UnityEngine.UI`, `Unity.TextMeshPro` | |
| `AutoService.Bootstrap` | Domain, Services, Infrastructure, Presentation | |
| `AutoService.Bootstrap.Editor` | Bootstrap | `includePlatforms: ["Editor"]` |
| `AutoService.Tests.EditMode` | Domain, Services, `UnityEngine.TestRunner`, `UnityEditor.TestRunner` | `includePlatforms: ["Editor"]`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` |

> Why `Services`, а не `Application`: namespace `AutoService.Application` перекрыл бы `UnityEngine.Application` во всех файлах внутри `AutoService.*`.

## 4. Контракты

Namespace = сборка + папка (например, `AutoService.Domain.Economy`).

### 4.1 Domain

**`Domain/Common/Money.cs`** — `public readonly struct Money : IEquatable<Money>, IComparable<Money>`
- Инвариант: сумма **неотрицательна** (`long`, целые доллары).
- `public static readonly Money Zero`
- `public long Amount { get; }`
- `public Money(long amount)` — `ArgumentOutOfRangeException`, если `amount < 0`.
- Операторы: `+` (с `checked`, при переполнении — насыщение до `long.MaxValue`), `-` (`InvalidOperationException`, если результат < 0), `*(Money, double multiplier)` (multiplier ≥ 0, округление `MidpointRounding.AwayFromZero`, насыщение), `==`, `!=`, `<`, `>`, `<=`, `>=`.
- `Equals`, `GetHashCode`, `CompareTo`, `ToString()` → `Amount` в `InvariantCulture` (только для отладки; для UI — `MoneyFormatter`).

**`Domain/Economy/Wallet.cs`** — `public sealed class Wallet`
```csharp
public Wallet(Money initialBalance);
public Money Balance { get; }
/// Raised after any balance change with the new balance.
public event Action<Money> BalanceChanged;
public bool CanAfford(Money cost);
public bool TrySpend(Money cost);   // false and no change if not enough; Zero → true, no event
public void Add(Money amount);      // Zero → no event
```

**`Domain/Economy/PriceFormula.cs`** — `public static class PriceFormula` (чистая функция, без состояния)
```csharp
/// cost = baseCost * growth^level, rounded, saturated to long.MaxValue.
public static Money Evaluate(Money baseCost, double growth, int level);
// growth < 1 или level < 0 → ArgumentOutOfRangeException
```

### 4.2 Services

**`Services/Core/`**
```csharp
public interface IInitializable { void Initialize(); }
public interface ITickable { void Tick(float deltaTime); }

public interface ITimeProvider { DateTime UtcNow { get; } }

public interface IRandom
{
    float Value();                                   // [0, 1)
    int Range(int minInclusive, int maxExclusive);
}

public interface IGameLogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
}

/// Reference-counted pause: nested popups can each request pause safely.
public interface IPauseService
{
    bool IsPaused { get; }
    event Action<bool> PausedChanged;
    void Push();   // count++ ; 0→1 pauses
    void Pop();    // count-- ; 1→0 resumes; Pop at 0 → logger warning, no-op
}
```

**`Services/Events/`**
```csharp
public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : struct;
    void Unsubscribe<T>(Action<T> handler) where T : struct;
    void Publish<T>(in T gameEvent) where T : struct;
}
```
`EventBus : IEventBus` (ctor: `IGameLogger`):
- Внутри `Dictionary<Type, object>` → приватный вложенный `Channel<T>` со списком обработчиков и **кешированным массивом-снимком**.
- `Publish` итерирует снимок по индексу → **ноль аллокаций**, безопасно подписываться/отписываться прямо внутри обработчика (изменение пересоздаёт снимок, текущая рассылка идёт по старому).
- Исключение в обработчике ловится, логируется через `IGameLogger.Error`, рассылка продолжается.
- Повторная подписка того же делегата игнорируется.

**`Services/Economy/`**
```csharp
/// Application-level facade over the domain Wallet.
public interface IWalletService
{
    Money Balance { get; }
    event Action<Money> BalanceChanged;
    bool CanAfford(Money cost);
    bool TrySpend(Money cost);
    void Add(Money amount);
}
```
`WalletService : IWalletService, IDisposable` (ctor: `Wallet`, `IEventBus`):
- Пробрасывает `Wallet.BalanceChanged` в своё событие **и** публикует в шину `BalanceChangedEvent`.
- `Dispose` отписывается от `Wallet`.

`Services/Economy/BalanceChangedEvent.cs` — `public readonly struct BalanceChangedEvent { Money Balance; long Delta; }` (Delta > 0 — доход, < 0 — трата; нужен для звука/онбординга позже).

**`Services/Formatting/MoneyFormatter.cs`** — `public static class` (чистая функция)
```csharp
/// "$0", "$950", "$1.2K", "$12.3K", "$123K", "$1.23M", "$4.5B", "$1.2T".
/// Values < 1000 exact. Otherwise 3 significant digits, TRUNCATED (never shows more than the player has),
/// trailing zeros removed ("$1K", not "$1.00K"). InvariantCulture.
public static string Format(Money money);
```

**`Services/Config/`**
```csharp
/// Read-only game configuration exposed to the game in engine-agnostic form.
public interface IConfigProvider
{
    EconomySettings Economy { get; }
}

/// Immutable economy settings.
public sealed class EconomySettings
{
    public EconomySettings(Money startingMoney);
    public Money StartingMoney { get; }
}
```
По мере развития сюда добавятся секции (точки, машины, уровни). Домен и Services **никогда** не видят ScriptableObject.

### 4.3 Infrastructure

- `Infrastructure/Time/SystemTimeProvider : ITimeProvider` — `DateTime.UtcNow`.
- `Infrastructure/Random/SystemRandom : IRandom` — обёртка над `System.Random`; два ctor: без аргументов и `(int seed)`. (Имя без `Unity`, чтобы не путать с `UnityEngine.Random`.)
- `Infrastructure/Logging/UnityGameLogger : IGameLogger` — `Debug.Log / LogWarning / LogError`.
- `Infrastructure/Pause/TimeScalePauseService : IPauseService` (ctor: `IGameLogger`) — счётчик; при паузе `Time.timeScale = 0f`, при снятии `= 1f`.
- `Infrastructure/Config/GameConfig : ScriptableObject`
  - `[CreateAssetMenu(menuName = "AutoService/Game Config", fileName = "GameConfig")]`
  - `[SerializeField] private EconomySection _economy;` где `EconomySection` — `[Serializable]` класс с `[SerializeField, Min(0)] private long _startingMoney = 100;` + `[Tooltip]`.
  - Геттеры только для чтения. Это корневой конфиг: в будущем он будет ссылаться на другие SO-конфиги.
- `Infrastructure/Config/ScriptableObjectConfigProvider : IConfigProvider` (ctor: `GameConfig`) — **один раз** в конструкторе маппит SO → `EconomySettings`; `null`-конфиг → `ArgumentNullException`.

### 4.4 Bootstrap

**`Bootstrap/ServiceContainer.cs`** — `public sealed class ServiceContainer : IDisposable`
```csharp
public ServiceContainer(ServiceContainer parent = null);
/// Registers an instance under contract T. Throws if T already registered in THIS container.
public void Register<T>(T instance) where T : class;
/// Resolves T from this container or its parents. Throws InvalidOperationException naming the missing type.
public T Resolve<T>() where T : class;
public bool TryResolve<T>(out T instance) where T : class;
/// Disposes every registered IDisposable exactly once (same instance under several contracts → once), in reverse registration order.
public void Dispose();
```
В XML-summary явно напиши: *контейнер используется только в EntryPoint'ах; остальной код получает зависимости через конструктор — это Composition Root, а не Service Locator.*

**`Bootstrap/GameLoop.cs`** — `public sealed class GameLoop : MonoBehaviour`
```csharp
public void Add(ITickable tickable);
public void Remove(ITickable tickable);
```
- Хранит `ITickable[]` + счётчик (растёт удвоением), `Update()` → `Tick(Time.deltaTime)` по индексу. Это **единственный** геймплейный `Update`.
- Добавление/удаление во время тика **отложенное**: применяется в начале следующего `Update` (без аллокаций в steady-state).
- Порядок тика = порядок добавления (задокументировать).

**`Bootstrap/ISceneEntryPoint.cs`**
```csharp
/// Implemented by the single root component of a scene that builds that scene's object graph.
public interface ISceneEntryPoint
{
    void Enter(ServiceContainer projectServices);
}
```

**`Bootstrap/ProjectEntryPoint.cs`** — `public sealed class ProjectEntryPoint : MonoBehaviour` (сцена `Boot`)
- `[SerializeField] private GameConfig _gameConfig;` `[SerializeField] private string _firstSceneName = "Gameplay";`
- `Awake`: `DontDestroyOnLoad(gameObject)`; создаёт project-контейнер и регистрирует: `IGameLogger`, `ITimeProvider`, `IRandom`, `IPauseService`, `IEventBus`, `IConfigProvider`.
- `Start` (async, `Awaitable`/`await SceneManager.LoadSceneAsync(...)`; если await на `AsyncOperation` не компилируется — корутина): грузит `_firstSceneName` (`LoadSceneMode.Single`), затем ищет среди `scene.GetRootGameObjects()` компонент `ISceneEntryPoint` (`TryGetComponent`) и вызывает `Enter(projectContainer)`. Не нашёл → `IGameLogger.Error`. Исключения в async-методе — ловить и логировать.
- `OnDestroy`: `Dispose` контейнера.
- Пометь `// TODO(09-scenes-ui): replace with ISceneLoader + loading screen.`

**`Bootstrap/GameplayEntryPoint.cs`** — `public sealed class GameplayEntryPoint : MonoBehaviour, ISceneEntryPoint` (сцена `Gameplay`)
- `[SerializeField] private GameLoop _gameLoop;`
- `Enter(parent)`:
  1. `_container = new ServiceContainer(parent)`;
  2. `var config = parent.Resolve<IConfigProvider>()`;
  3. `var wallet = new Wallet(config.Economy.StartingMoney)`;
  4. регистрирует `IWalletService` → `new WalletService(wallet, eventBus)`;
  5. вызывает `Initialize()` у всех созданных `IInitializable` (пока их нет — заложи место/метод), добавляет `ITickable` в `_gameLoop`;
  6. логирует `"[Gameplay] Ready. Balance: " + MoneyFormatter.Format(...)` через `IGameLogger`.
- `OnDestroy`: `_container?.Dispose()`.
- Раздели код на приватные методы по шагам (`RegisterEconomy`, `InitializeServices`, …) — сюда будут добавляться модули.

**`Bootstrap/Editor/PlayModeStartScene.cs`** — `[InitializeOnLoad] internal static class`
- Выставляет `EditorSceneManager.playModeStartScene` = `Assets/_Project/Scenes/Boot.unity`, чтобы Play из любой открытой сцены стартовал через Boot. Сцена не найдена → `Debug.LogWarning` с подсказкой.

## 5. Тесты (EditMode, NUnit)

`Tests/EditMode/`:
- `MoneyTests` — отрицательный ctor бросает; сложение/насыщение; вычитание в минус бросает; умножение и округление; сравнения/равенство.
- `WalletTests` — `TrySpend` при нехватке → false и без события; успешная трата → событие с новым балансом; `Add(Zero)` без события.
- `PriceFormulaTests` — level 0 = base; рост; невалидные аргументы; насыщение при огромном level.
- `EventBusTests` — доставка; отписка; отписка внутри обработчика не ломает рассылку; исключение в одном обработчике не мешает остальным (фейковый `IGameLogger`); повторная подписка не дублирует.
- `MoneyFormatterTests` — таблица: 0, 999, 1000, 1234, 12345, 123456, 1_234_567, 999_999 (→ "$999K", не "$1M"), 4_500_000_000.

## 6. Уборка проекта

- Удалить шаблонный мусор: `Assets/TutorialInfo/`, `Assets/Readme.asset`, `Assets/Scenes/SampleScene.unity`, `Assets/Settings/SampleSceneProfile.asset` (+ их `.meta`).
- Из `Packages/manifest.json` удалить: `com.unity.visualscripting`, `com.unity.multiplayer.center`, `com.unity.collab-proxy` (не нужны; меньше вопросов у проверяющего).
- `Assets/InputSystem_Actions.inputactions` **не трогать** — разберёмся в промпте 02.

## 7. Editor setup (делает пользователь руками после мержа кода)

> Кодер: включи этот список в описание PR.

1. Дождаться компиляции, убедиться, что Console без ошибок.
2. `Assets/_Project/Configs` → ПКМ → *Create → AutoService → Game Config* → `GameConfig`. Starting Money = 100.
3. Создать сцену `Assets/_Project/Scenes/Boot.unity`: пустой GO `ProjectEntryPoint` + компонент `ProjectEntryPoint`, назначить `GameConfig`, First Scene Name = `Gameplay`. Удалить из сцены камеру и свет (Boot пустая).
4. Создать сцену `Assets/_Project/Scenes/Gameplay.unity`: оставить Main Camera + Directional Light; пустой GO `[EntryPoint]` с компонентами `GameplayEntryPoint` и `GameLoop`; перетащить `GameLoop` в поле `GameplayEntryPoint._gameLoop`.
5. *File → Build Profiles → Scene List*: убрать SampleScene, добавить `Boot` (индекс 0) и `Gameplay` (1).
6. Открыть `Gameplay`, нажать Play → должен стартовать Boot, загрузиться Gameplay, в Console: `[Gameplay] Ready. Balance: $100`.
7. *Window → General → Test Runner → EditMode → Run All* — всё зелёное.
8. **Закоммитить в ту же ветку** `feature/01-core` всё, что создал Unity и ты: сгенерированные `.meta` для новых файлов/папок, сцены `Boot`/`Gameplay`, `GameConfig.asset`, изменённые `ProjectSettings/EditorBuildSettings.asset` и `Packages/packages-lock.json`. Только после этого — мерж PR.

> Why: кодер создаёт `.cs`/`.asmdef` без `.meta`. GUID'ы генерирует Unity у пользователя один раз — если их не закоммитить, ссылки на скрипты в сценах/префабах сломаются у любого, кто склонирует репозиторий (включая проверяющего).

## 8. Критерии приёмки

- [ ] Проект компилируется без ошибок и без warning'ов в нашем коде.
- [ ] Попытка написать `using UnityEngine;` в Domain/Services не компилируется (проверено `noEngineReferences`).
- [ ] Нет синглтонов/статического изменяемого состояния (кроме Editor-утилиты).
- [ ] Play из любой сцены → Boot → Gameplay → лог баланса `$100`.
- [ ] Все EditMode-тесты зелёные.
- [ ] XML-доки на всём публичном API.
- [ ] Удалён шаблонный мусор и лишние пакеты.

## 9. Git

- Ветка `feature/01-core`, осмысленные коммиты (например: `Add assembly structure`, `Add domain economy`, `Add core services and event bus`, `Add bootstrap and game loop`, `Add EditMode tests`, `Remove template assets and unused packages`).
- PR в `main`: заголовок `01: Core, economy, bootstrap`; в описании — что сделано, **Editor setup** из §7, отклонения от промпта (если были) с причиной.
- **Не** коммитить `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln` (уже в `.gitignore`).
