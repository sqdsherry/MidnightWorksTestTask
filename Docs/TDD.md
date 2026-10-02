# TDD — Auto Service Tycoon

> Живой документ. Как устроено и почему. Геймдизайн — в [GDD.md](GDD.md).
> Статусы: ✅ решено · 🟡 черновик · ❓ открытый вопрос · ⏳ не начато · 🔨 в работе · ✔️ готово

**Последнее обновление:** 2026-09-30
**Unity:** 6000.3.17f1 · **RP:** URP 17.3 · **Пакеты:** AI Navigation 2.0, Input System 1.19, uGUI, Test Framework

---

## 0. Жёсткие правила ✅

1. **Никаких сторонних библиотек.** Нет Zenject/UniTask/DOTween/Odin. Можно: всё из `com.unity.*`, `UnityEngine.Pool`, `Awaitable`, корутины.
2. **Домен не знает про Unity.** `Domain.asmdef` с `noEngineReferences: true` — компилятор не даст нарушить.
3. **Нет синглтонов, `FindObjectOfType`, `GameObject.Find`, статического состояния.** Все зависимости — через Composition Root.
4. **Ноль аллокаций в тике:** без LINQ, лямбд-замыканий, boxing, конкатенации строк, `GetComponent` в `Tick`/`Update`.
5. **Каждая подписка на event имеет отписку** (`Dispose` / `OnDestroy`).
6. **XML-комментарии (`///`)** на всём публичном API + комментарии «почему» в неочевидных местах. Это критерий оценки.

## 1. Слои и сборки ✅

```
Presentation ──► Services ──► Domain
     │               ▲
     ▼               │ реализует интерфейсы
Infrastructure ──────┘
Bootstrap ──► всё (только сборка графа)
```

> Слой Application (Clean Architecture) называется **Services**: namespace `AutoService.Application` конфликтовал бы с `UnityEngine.Application` во всех файлах внутри `AutoService.*`.

| Сборка (asmdef) | Namespace | Ссылки | Что внутри |
|---|---|---|---|
| `AutoService.Domain` | `AutoService.Domain.*` | — (`noEngineReferences`) | сущности, value objects, FSM, формулы |
| `AutoService.Services` | `AutoService.Services.*` | Domain (`noEngineReferences`) | интерфейсы сервисов, оркестраторы, EventBus, lifecycle-интерфейсы, конфиг-модели, DTO сейва |
| `AutoService.Infrastructure` | `AutoService.Infrastructure.*` | Domain, Services | JsonUtility-сейв, PlayerPrefs, SO-конфиги, время, рандом, пауза (`timeScale`) |
| `AutoService.Presentation` | `AutoService.Presentation.*` | Domain, Services, InputSystem, AI.Navigation, uGUI, TMP | MonoBehaviour: Views, NavMesh-агенты, камера, ввод, UI (MVP) |
| `AutoService.Bootstrap` | `AutoService.Bootstrap` | все | EntryPoint'ы, `ServiceContainer`, `GameLoop` |
| `AutoService.Bootstrap.Editor` | `AutoService.Bootstrap.Editor` | Bootstrap (Editor only) | старт Play Mode со сцены Boot |
| `AutoService.Tests.EditMode` | `AutoService.Tests.EditMode` | Domain, Services | юнит-тесты |

T1 решён: Services тоже `noEngineReferences` → весь геймплей тестируется в EditMode.

## 2. Структура папок ✅

```
Assets/_Project/
  Scripts/
    Domain/            (asmdef)
    Services/          (asmdef)
    Infrastructure/    (asmdef)
    Presentation/      (asmdef)
    Bootstrap/         (asmdef)
  Tests/EditMode/      (asmdef)
  Configs/             ScriptableObject-ассеты (баланс, точки, машины, уровни)
  Prefabs/  Scenes/  Art/  UI/  Audio/  Materials/
Docs/                  GDD.md, TDD.md
```

## 3. Core-инфраструктура 🟡

### 3.1 Composition Root
- `ServiceContainer` — рукописный: `Register<T>(T instance)`, `Resolve<T>()`. **Используется только внутри EntryPoint'ов.** Классы получают зависимости через конструктор (C#) или `Construct(...)` (MonoBehaviour). Никто, кроме EntryPoint, контейнер не видит — иначе это Service Locator.
- `ProjectEntryPoint` (сцена `Boot`, `DontDestroyOnLoad`) — глобальное: конфиги, время, рандом, пауза, (позже) сейв, настройки, загрузчик сцен, экран загрузки, аудио.
- `GameplayEntryPoint` (сцена `Gameplay`) — всё игровое. Порядок: конфиги → домен → сервисы → загрузка сейва → views/presenters → `Initialize()` → старт тика.
- **Передача контейнера без статики:** `ProjectEntryPoint` грузит сцену, ищет среди root-объектов компонент `ISceneEntryPoint` и вызывает `Enter(projectContainer)`. Сценовый контейнер — дочерний (`new ServiceContainer(parent)`), `Resolve` идёт вверх по родителям.
- Контейнер запоминает зарегистрированные `IDisposable` и диспоузит их в обратном порядке при выгрузке сцены.
- Editor: `PlayModeStartScene` всегда = Boot, чтобы Play работал из любой открытой сцены.

### 3.2 Жизненный цикл
- `IInitializable { void Initialize(); }`
- `ITickable { void Tick(float deltaTime); }`
- `IDisposable` (штатный).
- `GameLoop : MonoBehaviour` — **единственный** геймплейный `Update`. Держит `ITickable[]` (массив, не List + foreach по интерфейсу без аллокаций). Передаёт `Time.deltaTime` (scaled).

### 3.3 Пауза
`Time.timeScale = 0` → геймплей стоит сам (dt = 0). UI-анимации — на `unscaledDeltaTime`. `IPauseService` со счётчиком запросов (несколько попапов подряд не ломают паузу).

### 3.4 События
Два механизма, чётко разделены:
- **C# `event Action<...>` на сервисе/сущности** — когда подписчик знает источник (Presenter ↔ свой сервис).
- **`IEventBus`** — сквозные доменные события, которые слушают многие (XP, онбординг, облачка, звук):
  `Subscribe<T>(Action<T>)`, `Unsubscribe<T>(Action<T>)`, `Publish<T>(in T evt) where T : struct`.
  События — `readonly struct` → без аллокаций. Примеры: `CarServiced`, `CarLeftAngry`, `OrderAccepted`, `PointBroken`, `LevelUp`, `BuildCompleted`.

### 3.5 Абстракции окружения
`ITimeProvider` (UTC now), `IRandom` (`float Value()`, `int Range(int,int)`) — для детерминированных тестов.

## 4. Модули 🟡

> Контракты здесь — ориентир для промптов кодеру, не финальный код.

### 4.1 Economy
- `Money` — `readonly struct` над `long`, операторы, `ToString` через форматтер (1.2K/3.4M).
- `Wallet` (домен): `Balance`, `bool TrySpend(Money)`, `void Add(Money)`, `event Action<Money> Changed`.
- `IWalletService` (Services) — обёртка + публикация событий.
- `PriceFormula` — `Cost(base, growth, level)`.

### 4.2 Config
- SO в Infrastructure: `ServicePointConfig`, `CarTypeConfig`, `SupplyConfig`, `LevelTableConfig`, `BuildableConfig`, `StaffConfig`, `VipConfig`, `BalanceConfig`.
- `IConfigProvider` отдаёт **доменные readonly-структуры/классы** (маппинг SO → домен), домен SO не видит.
- Ключи — строковые `Id` в конфиге (стабильны для сейва). ❓ T2: string vs int.

### 4.3 Service Points (сердце игры)
Доменная сущность `ServicePoint`:
- `Id`, `ServiceType`, `LocationId`
- `Levels` (speed, price, reliability)
- `Supply { Current, Max, SupplyType }` (у шлагбаума — нет)
- `State`: `Idle | AwaitingAccept | Servicing | Broken`
- `Occupant`: `None | Player | Worker`
- `CurrentOrder` (`Order { CarId, Price, Progress, IsVip }`)
- `Tick(dt)` двигает прогресс **только если** `Occupant != None && State == Servicing`.
- События: `OrderAccepted`, `ServiceCompleted`, `Broke`, `Repaired`, `SupplyChanged`.

Шлагбаум — тот же `ServicePoint` с `ServiceType.Parking`, без расходника, завершение = машина встаёт на место.

`IServicePointService` — реестр точек, поиск свободной точки по типу.

### 4.4 Parking & Dispatch
- `EntryQueue` (домен): очередь-полоса на дороге из N слотов перед развилкой. `bool TryEnqueue(car)`, `Head`, `Dequeue()`; при сдвиге машины подтягиваются на слот вперёд. Полная → машина проезжает мимо.
- `ParkingLot` (домен): слоты, `bool TryReserve(out slot)`, `Release(slot)`.
- `OrderDispatcher` (Services, `ITickable`): для головы очереди — свободна нужная точка → сразу на неё; иначе → к шлагбауму. Для парковки — FIFO сопоставление машин со свободными точками.

### 4.5 Cars (AI клиентов)
- Домен: `Car` — `Type`, `RequestedService`, `Patience`, `IsVip`, FSM:
  `Arriving → InQueue → (ToPoint | AtBarrier → ToParking → Parked → ToPoint) → AtPoint → Leaving (paid | angry | noSpace)`.
- Presentation: `CarView` — `NavMeshAgent`, по прибытии сообщает домену `OnArrived()`. Домен про NavMesh не знает.
- `CarSpawner` (Services, `ITickable`) решает *когда и какую* машину создать и зовёт `ICarViewFactory` (интерфейс в Services). Реализация фабрики — в Presentation, на `UnityEngine.Pool.ObjectPool<CarView>` (Services Unity не видит).
- NavMesh: отдельный **agent type «Car»** (радиус больше), дорожная area. Финальная доводка на слот — плавный Lerp к позе слота. ❓ T3.

### 4.6 Player Character
- Домен: `PlayerState` FSM (`Idle/Moving/Working/Repairing`) + `CarriedBox?`.
- Presentation (модуль 02): `GameplayInput`, `PointerRaycaster`, `IInteractable` (`ApproachPosition/Rotation`, `BeginInteraction/EndInteraction`), `PlayerView` + `PlayerMotor : ITickable` (FSM движения), `PlayerInputPresenter`, `CameraRig`.
- Модуль 03+: точки/склад реализуют `IInteractable`; `BeginInteraction` → Services по **id точки** (`occupy(pointId, Occupant.Player)` / взять ящик / ремонт). Services не видит Unity-типов.

### 4.7 Staff
- `Worker` — привязан к точке, FSM `Spawn → GoToSpot → Working (→ Repairing slowly)`.
- `Storekeeper` — на локацию, FSM `Idle → ToWarehouse → Carry → ToPoint → Deliver`; цель — точка с min `Supply.Current / Max`.
- `IStaffService` — найм, лимиты, стоимость.

### 4.8 Warehouse & Supplies
- `Warehouse` (на локацию): `bool TryTakeBox(SupplyType, out Box)` — списывает $ через `IWalletService`.
- Выбор типа ящика при клике — `SupplyPriorityPolicy` (точка, где кончится раньше). Q1 в GDD.

### 4.9 Breakdowns
- `BreakdownService`: на `ServiceCompleted` → `IRandom` vs `chance(reliabilityLevel)` → `point.Break()`.
- Ремонт: `RepairProgress` на точке; игрок — 2 с hold, работник — ~10 с.

### 4.10 VIP & Negotiation
- `VipService`: таймер спавна × `Loyalty`, флаг `IsVip` на машине.
- `NegotiationService` (домен-логика): `Resolve(option, IRandom) → NegotiationResult { Accepted, Multiplier }`.
- Presentation: `VipPopupPresenter`, `NegotiationPopupPresenter` (запрашивают паузу).

### 4.11 Build
- `BuildPlot` (домен): `Id`, `BuildableId`, `State: Locked | Available | Built`, `RequiredLevel`, `Cost`.
- `IBuildService`: `CanBuild(id)`, `bool TryBuild(id)`, `event Action<string> Built`.
- Presentation: `BuildPlotView` (призрак, ценник), на `Built` — инстанс префаба точки + анимация.
- **Новый тип постройки = новый конфиг + префаб.** Код сервиса не меняется.

### 4.12 Progression
- `PlayerProgress`: `Xp`, `Level`, `AddXp(int)`, `event LevelUp`.
- `UnlockService` — по `LevelTableConfig` открывает `BuildPlot`/найм; слушает `LevelUp`.

### 4.13 Onboarding
- `TutorialService`: последовательность шагов, каждый ждёт своё событие из `IEventBus`. Прогресс сохраняется.
- Presentation: `TutorialArrowView` + подсказка в HUD.

### 4.14 Speech Bubbles
- `BubbleService` слушает `IEventBus` → `BubbleView` из пула над целью. Тексты — в конфиге.

### 4.15 Save
- `ISaveService`: `bool TryLoad(out SaveData)`, `void Save(SaveData)`, `void Delete()`.
- `SaveData` — `[Serializable]`, поле `version`, списки (JsonUtility не умеет Dictionary → массивы пар).
- Сохраняем: деньги, XP/уровень, постройки (built, уровни апгрейдов), найм, расходники на точках, лояльность VIP, шаг онбординга, открытые локации.
- **Не сохраняем:** машины в пути, положение персонажа (стартует у склада).
- Запись атомарная: `save.tmp` → `File.Replace`/move. Путь: `Application.persistentDataPath/save.json`.
- Автосейв: раз в 30 с + `OnApplicationPause(true)` + `OnApplicationQuit` + после постройки/найма.
- `ISaveMapper` / `ISaveable` — каждый сервис сам пишет/читает свой кусок `SaveData` (❓ T4).
- Настройки — отдельно, `PlayerPrefs` через `ISettingsService`.

### 4.16 Scene Flow & Loading
- Сцены: `Boot` → `MainMenu` → `Gameplay`.
- Экран загрузки — **persistent canvas** в `Boot` (не отдельная сцена): `ISceneLoader.LoadAsync(name)` на `SceneManager.LoadSceneAsync` + `Awaitable`, показывает прогресс.

### 4.17 Input
- Input System, свой `.inputactions`: `Point`, `Click` (+ hold для ремонта), `Pan` (WASD), `Zoom` (scroll), `Recenter` (Space), `Cancel` (Esc).
- `PointerRaycaster`: `Physics.RaycastNonAlloc`, слои `Interactable` / `Ground`; блок, если над UI (`EventSystem.IsPointerOverGameObject`).
- Цели клика реализуют `IInteractableTarget` (Presentation) → транслируют в `PlayerCommandService`.

### 4.18 Camera
- `CameraRig`: follow с демпфированием, pan WASD/край экрана, zoom, границы (bounds на локацию), переключение локаций.

### 4.19 UI (MVP)
- **uGUI** (не UI Toolkit) — нужен world-space UI, проще единый стек. ✅
- `View` — пассивный MonoBehaviour (кнопки, тексты, `event Action` на клики).
- `Presenter` — чистый C#, подписан на сервисы, обновляет View **по событиям, не каждый кадр**.
- `IScreenService` — стек экранов/попапов.
- Числа — через кешированный форматтер, без аллокаций на каждый кадр.
- Канвас: `Scale With Screen Size`, 1920×1080, match 0.5.

### 4.20 Audio
`IAudioService`: `PlaySfx(id)`, музыка; громкость из `ISettingsService` → `AudioMixer` параметры.

## 5. Производительность ✅
- Пулы: машины, облачка, «+$»-всплывашки, NPC (❓).
- Кеш `WaitForSeconds`, `NonAlloc`-API физики.
- Никаких `Update` кроме `GameLoop`, `CameraRig` (LateUpdate), UI-анимаций.
- NavMesh: `NavMeshSurface` на локацию, obstacle у построек → перепечь при постройке (или carve ❓ T5).

## 6. Тесты 🟡
EditMode: `Wallet`, `PriceFormula`, `ServicePoint` (прогресс только при occupant, расходник, поломка), `ParkingLot`, `OrderDispatcher`, `PlayerProgress`/unlocks, `NegotiationService` (фейковый `IRandom`), маппинг сейва (round-trip).

## 7. Код-стайл ✅
- **Код и комментарии — на английском.** Документация проекта (Docs/) — на русском.
- Приватные поля `_camelCase`, `[SerializeField] private`, никаких public-полей.
- Один класс — один файл, имя файла = имя класса.
- `sealed` по умолчанию для не-базовых классов.
- XML-доки на всё публичное; `// Why:` на неочевидное.
- Коммиты — маленькие, по модулю.

---

## 🔀 Процесс разработки ✅

**Роли:** архитектор (Claude, этот чат) пишет промпт → **кодер-чат пишет весь код** → ревью делают вместе пользователь и архитектор → пользователь собирает сцену/префабы в Editor.

**Git-флоу:**
1. `main` — всегда рабочий, прямых коммитов нет (кроме Docs).
2. На каждый пункт roadmap — ветка `feature/<NN>-<name>` (напр. `feature/01-core`).
3. Кодер коммитит в ветку → открывается **PR в main**.
4. Ревью: архитектор читает diff (`git fetch` + `git diff main...origin/feature/<NN>-<name>`; `gh` CLI не установлен), сверяет с TDD и правилами §0, пишет замечания; пользователь проверяет в Editor.
5. Есть проблемы → архитектор пишет промпт-фикс (`Docs/Prompts/NNx-*.md`) в ту же ветку. Всё чисто → архитектор даёт пользователю Editor setup и список «что проверить в Play». Пользователь делает **только** работу в Editor и пишет «ок, мерджи». **Архитектор сам коммитит** в ветку `.meta`, сцены, SO-ассеты, материалы, префабы (`Add Editor setup: …`), делает **merge-коммит** (`--no-ff`, `commit-tree`) в main, пушит и удаляет ветку. **Без ревью не мержить никогда.** `gh` не установлен — PR на GitHub необязателен.
6. **Каждый модуль — новая сессия кодера** (свежий контекст; правила подтягиваются из `CLAUDE.md`).
7. Архитектор обновляет статус в roadmap и, если что-то поменялось, TDD/GDD.

**Формат каждого промпта кодеру:**
1. Контекст и цель модуля (+ ссылка на разделы GDD/TDD).
2. Файлы/классы/интерфейсы с контрактами (сигнатуры, события).
3. Правила реализации (§0 + специфичное для модуля).
4. Интеграция в EntryPoint.
5. **Editor setup** — по умолчанию **автоматизирован**: пункт меню `AutoService/Setup/Run <модуль> Setup` (Editor-утилиты создают объекты и UI, раскладку, назначают ссылки через `SerializedObject`, пекут NavMesh, валидируют, сохраняют сцену). Руками — только визуальная доводка. Если ручные шаги всё же нужны — каждый объект, размер, anchor/pivot, поле. View-панели: `Show()` сам включает цепочку объектов (состояние в сцене не должно влиять). Ниже — старый формат ручных шагов: что пользователь делает руками в Unity (объекты, компоненты, слои, NavMesh, ссылки в инспекторе). **Для каждого компонента явно писать, на КАКОЙ объект он вешается** (урок 02: компоненты попали на дочерний ApproachPoint вместо куба). Иерархию давать деревом.
6. Критерии приёмки (что должно работать/какие тесты зелёные).
7. Ветка и сообщение коммита.

> Узкое место — ручная сборка в Editor, а не объём кода. Поэтому: максимум `[SerializeField]`-ссылок и префабов, минимум «магии» и поиска объектов в рантайме; whitebox (кубы) до замены на ассеты.

## 🗺️ Roadmap (3–4 дня)

| # | Ветка | Модуль | День | Приоритет | Статус |
|---|---|---|---|---|---|
| 01 | `feature/01-core` | Структура, asmdef, Core (контейнер, GameLoop, EventBus, Pause, Time/Random), Economy, Config-база — [промпт](Prompts/01-core.md) | 1 | M | ✔️ смержен |
| 02 | `feature/02-player` | Input + Camera + Player click-to-move (whitebox сцена) — [промпт](Prompts/02-player.md) | 1 | M | ✔️ смержен |
| 03 | `feature/03-service-loop` | Service Points + Queue + Parking + Traffic + Cars AI + пул + мини-HUD баланса — [промпт](Prompts/03-service-loop.md) | 1–2 | M | ✔️ смержен |
| 03b | `feature/03b-traffic-routing` | Трафик v3: граф дорог, зоны слияния, 2 въездных шлагбаума с оплатой за время, буферы боксов, планы визита — [v2](Prompts/03b-traffic-routing.md) → [v3](Prompts/03b2-layout-v3.md) | Д1 | M | ✔️ смержен |
| R1 | — | Рефакторинг: `GameplayEntryPoint` → инсталлеры по модулям — **шаг 0 модуля 05** | 2 | S | ↪ в 05 |
| 04 | `feature/04-build` | **A1** Стройка: ряд 4 боксов с буферами, места парковки, рост потока, панель постройки — [промпт](Prompts/04-a1-build.md), [фикс](Prompts/04c-a1-setup-tools.md) | Д1 | M | ✔️ смержен |
| 05 | `feature/05-staff-supplies` | **A2** R1-инсталлеры + персонал (работник точки, кладовщик), склад/расходники/перенос, ManagePad + панель точки + апгрейды — [промпт](Prompts/05-a2-staff-supplies.md), фиксы [05b](Prompts/05b-a2-review-fixes.md), [05c](Prompts/05c-a2-playtest-fixes.md) | Д2 | M | ✔️ смержен |
| 07 | `feature/07-progression` | Progression (XP/уровень — **пишет пользователь** с подсказками архитектора) + Unlocks (кодер) | 2 | M | ⏳ |
| 08a | `feature/08a-save-core` | Ядро сейва (SaveData, атомарная запись, SaveCoordinator, автосейв) + Settings — [промпт](Prompts/08a-save-core.md) | Д1 | M | ✔️ смержен |
| 08b | `feature/08b-save-wiring` | Подключение сервисов к сейву (`ISaveable`), загрузка на старте, сейв при выходе | 2 | M | ⏳ |
| 09 | `feature/09-scenes-ui` | Scene Flow + Loading + MainMenu + Settings + HUD + панели/попапы | 3 | M | ⏳ |
| 10 | `feature/10-onboarding` | Onboarding | 3 | M | ⏳ |
| 11 | `feature/11-location2` | 2-я локация + переключение камеры | 3 | M | ⏳ |
| 12 | `feature/12-visual` | Ассеты, стиль, звук, juice | 3 | M | ⏳ |
| 13 | `feature/13-vip` | VIP + Negotiation | 4 | S | ⏳ |
| 14 | `feature/14-incidents` | Инциденты «удержи, чтобы решить»: поломки точек + мусор на парковке + уборщик; терпение/уход злых | 4 | S | ⏳ |
| 15 | `feature/15-debug-bubbles` | **Debug-панель F1 [M]** (+$, +уровень, спавн VIP, сброс сейва); облачки-реплики [C] | 3–4 | M/C | ⏳ |
| 16 | `feature/16-release` | README, чек-лист TZ.md, багфикс | 4 | M | ⏳ |

> Урезание под срок: тесты — только ключевой домен (Wallet, ServicePoint, ParkingLot, Save round-trip). Терпение как счётчик заложить в 03, уход злых — в 14.

## 🐞 Известные проблемы (чинить в пакете D / полировке)

| # | Проблема | Причина | Идея решения |
|---|---|---|---|
| K1 | Машины **обрывисто дёргаются при парковке** (и на точках) | на финальном узле агент резко тормозит, затем доворот на месте с 360°/с | узел-«подход» перед местом (заезд по прямой), плавное торможение (`autoBraking`, меньший `alignSpeed`, доворот во время подъезда), `Quaternion.Slerp` с демпфированием |
| K2 | Машины проезжают сквозь друг друга при подтягивании очереди | avoidance = None | дистанция до впереди идущей по пути (проверка занятости следующего узла) или avoidance Low для едущих машин |
| ~~K3~~ | ~~Подсветка шлагбаума теряет стрелу~~ | — | ✔️ исправлено в A1 |
| K4 | Персонаж может проходить сквозь стены построенного бокса | NavMesh печётся, пока боксы выключены | `NavMeshObstacle` (carve) на боксах (T5) — в A2 или D |

## ❓ Открытые технические вопросы

| # | Вопрос | Дефолт |
|---|---|---|
| ~~T1~~ | ~~Services без UnityEngine?~~ | ✅ Да, `noEngineReferences`. Векторы/позиции в Services не нужны — ими оперирует Presentation |
| T2 | Id конфигов: string или int? | string (читаемо в сейве и SO) |
| T3 | Парковка машин: чистый NavMesh или NavMesh + доводка Lerp на слот? | NavMesh до точки подъезда + Lerp |
| T4 | Сейв: центральный маппер или `ISaveable` на каждом сервисе? | `ISaveable` — сервисы независимы, легко добавлять |
| T5 | NavMesh при постройке: rebake или `NavMeshObstacle` carve? | carve (дёшево, без rebake) |
| T6 | Пул NPC нужен? | Нет, их мало — инстанс при найме |
| T7 | Вторая локация — та же сцена? | Да (GDD §6) |

## 💡 Тех-идеи — если успеем
- Editor-валидатор конфигов (дубли Id, пустые ссылки) — `OnValidate` / меню.
- Debug-панель (F1): +$1000, +уровень, спавн VIP, сброс сейва — очень поможет проверяющему.
- Миграции `SaveData` по `version`.
- PlayMode-тест «запуск Gameplay без ошибок».

## 📜 Лог тех-решений
| Дата | Решение |
|---|---|
| 2026-09-30 | DDD-слои, границы через asmdef, Domain `noEngineReferences` |
| 2026-09-30 | Рукописный контейнер только в EntryPoint'ах; один `GameLoop` |
| 2026-09-30 | `IEventBus` на struct-событиях + локальные C# events |
| 2026-09-30 | Пауза через `timeScale`, UI на unscaled |
| 2026-09-30 | uGUI, MVP |
| 2026-09-30 | Экран загрузки — persistent canvas в Boot |
| 2026-09-30 | Код пишет только кодер-чат; feature-ветки + ревью архитектора + merge-коммит `--no-ff` |
| 2026-10-02 | Протокол сдачи модуля: ревью → фикс-промпт или Editor setup + чек-лист → «ок» пользователя → архитектор коммитит Editor-файлы и мержит. Editor setup — одной кнопкой `AutoService/Setup` |
| 2026-09-30 | Срок 3–4 дня → roadmap из 16 веток, тесты только на ключевой домен |
| 2026-09-30 | Все UI-строки — английский, в конфигах/префабах, не хардкодом в логике |
| 2026-09-30 | Слой Application → **Services** (конфликт имён с `UnityEngine.Application`); Services тоже `noEngineReferences` |
| 2026-09-30 | Контейнер в сцену передаётся через `ISceneEntryPoint.Enter(parent)`, без статики; Play Mode всегда стартует с Boot |
| 2026-09-30 | Очередь-полоса `EntryQueue` перед развилкой точки/шлагбаум |
