# Архитектура — шпаргалка

> Как устроен проект, почему так, и как прочитать любой файл за 5 минут.
> Подробные контракты модулей — в [TDD.md](TDD.md). Здесь — «карта местности».

---

## 1. TL;DR за 30 секунд

- Игра собирается **в одном месте** — `Bootstrap/GameplayEntryPoint.cs`. Это оглавление: каждый метод `Register…()` = модуль игры.
- Классы получают зависимости **через конструктор**, ничего не знают о контейнере. Это **Pure DI / Composition Root** — то, что Zenject делает автоматически, мы делаем явно (сторонние DI-фреймворки запрещены ТЗ).
- Код разделён на **4 слоя**. Логика игры (Domain, Services) **не знает про Unity** — это проверяет компилятор.
- Вниз (Unity → логика) — **вызовы методов**. Вверх (логика → Unity) — **события**. Логика никогда не дёргает Unity напрямую.
- Каждый кадр — **один** `Update` (`GameLoop`), который тикает все `ITickable`.

## 2. Слои

```
Presentation ──► Services ──► Domain
Infrastructure ──► Services, Domain   (реализует интерфейсы Services)
Bootstrap ──► все (собирает граф)
```

| Слой | Отвечает на вопрос | Что внутри | Unity? | Пример |
|---|---|---|---|---|
| **Domain** | *Какие правила игры?* | сущности, формулы, состояния | ❌ | `ServicePoint`: «прогресс идёт, только если на рабочем месте кто-то стоит» |
| **Services** | *Кто кого вызывает и когда?* | оркестрация, интерфейсы, события шины | ❌ | `LocationTraffic`: «голова очереди едет на мойку, если она свободна» |
| **Infrastructure** | *Откуда данные / как работает платформа?* | конфиги (SO), пауза (`timeScale`), сейв | ✅ | `ScriptableObjectConfigProvider` |
| **Presentation** | *Как это выглядит и как этим управлять?* | MonoBehaviour, NavMesh, ввод, UI | ✅ | `CarView` двигает NavMeshAgent |
| **Bootstrap** | *Как всё собрать?* | EntryPoint'ы, контейнер, GameLoop | ✅ | `GameplayEntryPoint` |

**Почему Domain/Services без Unity:**
1. Логику можно тестировать за миллисекунды без сцены (`Tests/EditMode`).
2. Открыв файл, по папке сразу знаешь, чего ждать: в `Domain/` не будет `Transform`, в `Presentation/` не будет игровых правил.
3. Компилятор не даст случайно смешать (`noEngineReferences: true` в asmdef).

## 3. Карта папок

```
Assets/_Project/Scripts/
  Domain/
    Common/        Money — деньги (не уходят в минус, насыщаются при переполнении)
    Economy/       Wallet, PriceFormula
    Points/        ServicePoint — точка услуги/шлагбаум и её состояния        (03)
    Traffic/       Car, EntryQueue, ParkingLot                                (03)
  Services/
    Core/          ITickable, IInitializable, IPauseService, IRandom, ITimeProvider, IGameLogger
    Events/        IEventBus + EventBus
    Config/        IConfigProvider + неизменяемые настройки (без ScriptableObject)
    Economy/       IWalletService, BalanceChangedEvent
    Formatting/    MoneyFormatter ("$1.2K")
    Points/        IServicePointService, события заказов                       (03)
    Traffic/       LocationTraffic (оркестратор), ICarAgents (порт к машинам)  (03)
  Infrastructure/
    Config/        GameConfig (SO) → ScriptableObjectConfigProvider
    Pause/ Timing/ Randomness/ Logging/
  Presentation/
    Controls/      GameplayInput — обёртка над Input System
    Interaction/   IInteractable, PointerRaycaster, подсветка
    Player/        PlayerView, PlayerMotor (FSM ходьбы), PlayerInputPresenter
    CameraControl/ CameraRig
    Points/        ServicePointView, ServicePointHud, BarrierArm               (03)
    Traffic/       LocationLayout, CarView, CarAgents (пул машин)              (03)
    Hud/           BalanceView/Presenter                                       (03)
  Bootstrap/       ProjectEntryPoint, GameplayEntryPoint, ServiceContainer, GameLoop
Tests/EditMode/    юнит-тесты Domain/Services
```

## 4. Запуск игры

```
[Boot]      ProjectEntryPoint.Awake()            — глобальное: логгер, время, рандом, пауза, шина, конфиги
              └─ грузит сцену Gameplay, находит в ней ISceneEntryPoint
[Gameplay]  GameplayEntryPoint.Enter(project)    — сценовое, дочерний контейнер видит глобальное
              RegisterEconomy()  RegisterPlayer()  RegisterPoints()  RegisterTraffic()  RegisterHud() …
              InitializeServices()   → Initialize() у всех IInitializable
              StartTicking()         → все ITickable в GameLoop
            GameLoop.Update()        — каждый кадр Tick(dt) по всем, в порядке регистрации
            OnDestroy                — контейнер вызывает Dispose() у всех (отписки) в обратном порядке
```
Play из любой сцены в Editor всё равно стартует с Boot (`Bootstrap/Editor/PlayModeStartScene`).

## 5. Если ты знаешь Zenject

| Zenject | У нас |
|---|---|
| `ProjectContext` | `ProjectEntryPoint` (сцена Boot) |
| `SceneContext` + `MonoInstaller` | `GameplayEntryPoint` и его `Register…()` |
| `Container.Bind<IX>().To<X>().AsSingle()` | `Register<IX>(new X(dep1, dep2))` |
| `[Inject]` конструктор | обычный конструктор, аргументы передаём в `new` |
| `[Inject] Construct()` у MonoBehaviour | `Construct(...)`, вызывает EntryPoint |
| `ITickable` / `IInitializable` / `IDisposable` | те же интерфейсы (`Services/Core`) |
| `TickableManager` | `GameLoop` |
| `SignalBus` | `IEventBus` |
| `MemoryPool` | `UnityEngine.Pool.ObjectPool` |

Единственная разница: в Zenject `new` пишет контейнер, у нас — EntryPoint. `ServiceContainer` используется **только** внутри EntryPoint'ов; остальной код его не видит (иначе это был бы Service Locator — анти-паттерн).

## 6. Один клик насквозь (после модуля 03)

Игрок кликает по мойке, где ждёт машина:

```
1. GameplayInput            (Presentation)  ЛКМ → событие Clicked
2. PlayerInputPresenter     (Presentation)  луч из камеры → ServicePointView мойки → player.ApproachAndInteract
3. PlayerMotor              (Presentation)  дошёл по NavMesh, развернулся → view.BeginInteraction()
4. ServicePointView         (Presentation)  → pointService.TryOccupy("loc1_wash_1", Player)
   ════════ граница Unity: дальше чистый C# ════════
5. ServicePoint             (Domain)        Occupant = Player; в Tick: машина ждёт + исполнитель есть
                                            → событие OrderAccepted(цена)
6. ServicePointService      (Services)      → wallet.Add(цена); шина ← OrderAcceptedEvent
7. WalletService            (Services)      → событие BalanceChanged
   ════════ обратно в Unity — только через события ════════
8. BalancePresenter         (Presentation)  → текст "$112"
```

Машины идут тем же путём, но наоборот: `LocationTraffic` (Services) решает «машине 7 ехать на мойку» → вызывает **`ICarAgents.MoveTo(7, Point("loc1_wash_1"))`** → реализация в Presentation (`CarAgents`) находит трансформ на сцене и ставит цель NavMeshAgent → доехала → событие `Arrived(7)` → `LocationTraffic` продолжает сценарий. Логика не знает ни про NavMesh, ни про координаты.

## 7. Как прочитать любой файл

1. **Папка** → слой → роль (таблица §2).
2. **`/// <summary>` над классом** — 1–2 фразы «зачем».
3. **Конструктор** — зависимости (как `[Inject]`).
4. **Где создаётся** — поиск `new ИмяКласса` → почти всегда `GameplayEntryPoint`.
5. **Публичные методы и события** — это контракт; приватное читать в последнюю очередь.
6. `// Why:` — почему сделано неочевидно; при первом чтении можно пропускать.

## 8. Рецепты расширения

**Новый тип услуги (например, «Замена масла»)** — без кода:
1. `Create → AutoService → Service Type` → `ST_Oil` (цена, длительность), добавить в `GameConfig`.
2. На сцене продублировать точку мойки, поменять `pointId` и `serviceTypeId`, добавить в `LocationLayout`.
Машины сами начнут запрашивать новую услугу.

**Новый тип машины** — `Create → AutoService → Car Type` + префаб в `CarVisualCatalog`.

**Новая система (например, поломки)**:
1. Domain — правила (чистый C#) + тест.
2. Services — сервис, который связывает Domain с другими сервисами; события в шину.
3. Presentation — View (MonoBehaviour, только отображение) + Presenter (подписан на сервис).
4. `GameplayEntryPoint` — метод `RegisterBreakdowns()`.

## 9. Паттерны (если спросят названия)

| Что | Паттерн |
|---|---|
| Все `new` в EntryPoint, зависимости через конструктор | Composition Root / Pure DI (M. Seemann) |
| Слои с зависимостями «внутрь» | Clean Architecture (R. Martin) |
| MonoBehaviour только отображает, логика в C# | Humble Object |
| `ICarAgents` — интерфейс в Services, реализация в Unity | Ports & Adapters (A. Cockburn) |
| View + Presenter | MVP |
| `IEventBus`, C#-события | Observer / Pub-Sub |
| Один `Update` | Game Loop / Update Method (R. Nystrom) |
| Состояния персонажа, машины, точки | State Machine (enum + переходы в одном месте) |
| Пул машин | Object Pool |

## 10. Вопросы на собеседовании — и ответы

**Почему без DI-фреймворка?** ТЗ запрещает сторонние библиотеки. DI — это принцип, а не библиотека: Composition Root даёт то же самое (зависимости через конструктор, единое место сборки), только явно. Плюс — видно весь граф в одном файле; минус — `new` пишем руками.

**Зачем Domain и Services без UnityEngine?** Логика тестируется без сцены и Play Mode; слой нельзя случайно «запачкать» — это гарантирует `noEngineReferences` в asmdef.

**Зачем `ICarAgents`, почему не вызвать NavMeshAgent из `LocationTraffic`?** Тогда трафик (бизнес-логика) зависел бы от Unity и не тестировался бы. Интерфейс — граница: логика говорит «что», Presentation решает «как». В тестах подставляется фейк.

**Почему один `GameLoop` вместо `Update` в каждом классе?** Явный порядок обновления, ноль накладных расходов на сотни `Update`-вызовов из движка, логика (не MonoBehaviour) тоже может тикать, пауза через `dt = 0`.

**Почему C#-события и шина одновременно?** Событие на сервисе — когда подписчик знает источник (Presenter своего сервиса). Шина — для сквозных реакций, которым неважен источник (XP, звук, онбординг): так звук не зависит от десятка сервисов.

**Почему события-struct?** Публикация без аллокаций — не создаёт мусор для GC каждый раз.

**Как избежать утечек подписок?** Каждый класс с подпиской — `IDisposable`; контейнер диспоузит всё зарегистрированное при выгрузке сцены, в обратном порядке.

**Почему `Money` — struct над `long`, а не `float`?** Целые доллары без ошибок округления; `long` хватает на idle-числа; инвариант «не меньше нуля» в одном месте; переполнение насыщается, а не уходит в минус.

**Как добавить вторую локацию?** Второй `LocationLayout` на сцене + второй экземпляр `LocationTraffic`. Код трафика не меняется.

**Что бы сделал иначе в большом проекте?** Взял бы DI-контейнер (VContainer/Zenject) вместо ручных `new`, Addressables для контента, вынес бы конфиги в таблицы. Архитектура слоёв осталась бы той же.

## 11. Глоссарий

- **Точка (ServicePoint)** — место, где машину обслуживают: шлагбаум, мойка и т.д.
- **Рабочее место (WorkSpot / ApproachPoint)** — где стоит исполнитель (игрок или работник). Пока там никого — точка не работает.
- **Исполнитель (Occupant)** — `Player` или `Worker`.
- **Очередь-полоса (EntryQueue)** — машины на дороге перед развилкой.
- **Голова очереди** — первая машина у развилки, именно она решает, куда ехать.
- **Порт** — интерфейс в Services, который реализует Unity-слой (`ICarAgents`).
- **Presenter** — C#-класс, который слушает сервис и обновляет View.
- **View** — MonoBehaviour, который только показывает и сообщает о кликах.
