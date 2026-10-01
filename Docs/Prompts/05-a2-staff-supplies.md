# Промпт 05 (A2) — Персонал, склад и расходники, панель точки, апгрейды

**Ветка:** `feature/05-staff-supplies` (от `main` после мержа 04) · **PR:** в `main`
**Перед началом:** `CLAUDE.md`, `Docs/GDD.md` §4.2–4.3, §7, §8, §9, `Docs/TDD.md` §4.3, §4.7, §4.8, `Docs/ARCHITECTURE.md`, код A1 (`Domain/Common/DwellProgress`, `Services/Building`, `Presentation/Building`: `BuildPlotView`, `BuildPanelView`, `BuildPanelPresenter`, `BuildableBinder`).

---

## 1. Цель

Арка «всё руками → найм» (GDD §9.2):
- **Расходники.** У каждой точки услуги есть запас (ёмкость **10**, на старте полный). Каждый **принятый** заказ тратит 1 единицу. Пустой запас → машина стоит на точке, заказ не принимается, над точкой иконка ящика.
- **Склад** (один на локацию, есть со старта). Клик → персонаж подходит → **сразу** берёт ящик (**5** единиц) для самой «голодной» точки и платит за него. Несёт **1** ящик. Клик по точке с подходящим ящиком → ящик выгружается, игрок встаёт работать.
- **Найм.** Работник точки (по одному на точку) выходит из комнаты персонала, по NavMesh идёт к WorkSpot и занимает его навсегда. **Кладовщик** (один на локацию) сам носит ящики со склада к самой «голодной» точке.
- **Панель точки:** апгрейды *Speed* / *Price*, найм, статус (запас, работник), доход в минуту.
- **Визуальный язык «жёлтое — работа, синее — покупки»:**
  - 🟨 **WorkPad**: жёлтая площадка под WorkSpot. Клик — работать (как сейчас).
  - 🟦 **ManagePad**: синяя площадка со значком ⬆ рядом с точкой (≈2 м, не на полосе машин). Клик → подойти → **постоять 1.5 с** (кольцо) → панель рядом с объектом. Правило одно и то же, есть на точке работник или нет. У склада свой ManagePad, там нанимают кладовщика.
  - Призраки A1 перекрашиваются в синий тон (`M_Ghost` → голубоватый), чтобы стройка читалась как «покупка».

Требования к уровню у найма и апгрейдов идут через `IUnlockGate` из A1 (сейчас заглушка, всегда true).

## 2. Шаг 0 — R1: инсталлеры (отдельный коммит, без изменения поведения)

`GameplayEntryPoint` разбить на инсталлеры, по одному на модуль (`Bootstrap/Installers/`):
- `EconomyInstaller`, `PlayerInstaller`, `ServiceLoopInstaller` (точки + трафик), `BuildingInstaller`, `HudInstaller`; в A2 к ним добавится `StaffSuppliesInstaller`.
- Контракт:
  ```csharp
  internal interface IGameplayInstaller { void Install(GameplayContext context); }
  ```
  `GameplayContext` — это `ServiceContainer` сцены, `Register<T>` / `Track(object)` (lifecycle-списки остаются в EntryPoint), `IGameLogger` и сериализованные ссылки сцены (`GameplaySceneRefs`, `[Serializable]`-класс с полями, которые сейчас висят на EntryPoint).
- `GameplayEntryPoint.Enter` превращается в оглавление: создать контекст → вызвать инсталлеры по порядку → `InitializeServices` → `StartTicking`. Порядок тиков сохраняется.
- Инсталлеры — обычные C#-классы (`sealed`), не MonoBehaviour. Контейнер видят только они и EntryPoint. Это считается частью Composition Root, поэтому правило 3 из CLAUDE.md не нарушается; отметить это в XML-доке `GameplayContext`.
- **Сцена не меняется.** Сериализованные поля остаются на `GameplayEntryPoint` как есть (перенос во вложенный класс потерял бы ссылки в сцене). `Enter` собирает из них неизменяемый `GameplaySceneRefs` (обычный C#-класс, конструктор) и передаёт его в контекст. Пользователь ничего не переназначает в инспекторе.
- **Общая регистрация точки:** `GameplayEntryPoint.TryRegisterPoint` и `BuildableBinder.ActivatePoint` делают одно и то же (Register → `view.Construct` → `ServicePointPresenter`). Вынести в `PointRegistrar` (Presentation, C#), которым пользуются оба. В A2 к нему добавятся апгрейды, трекер дохода и `Construct` с supply/staff.
- Критерий: Play и тесты ведут себя так же, как до рефакторинга.

## 3. Domain

### 3.1 `Domain/Supplies/`
- `SupplyStock` (сущность): `string SupplyTypeId`, `int Current`, `int Capacity`, `bool IsEmpty`, `float Fill01`, `bool CanAdd(int units)` (= `Capacity - Current >= units`), `bool TryConsume()`, `void Add(int units)` (если не влезает → `InvalidOperationException`; вызывающий код проверяет `CanAdd`), `void Restore(int current)` (с clamp, для сейва), `event Action<SupplyStock> Changed`.
- `SupplyBox` (`readonly struct`): `string SupplyTypeId`, `int Units`; `static SupplyBox None`, `bool IsNone`.

### 3.2 `ServicePoint` (изменения)
- `ServicePointDefinition` получает `string SupplyTypeId` (пустая строка → расходника нет, это барьеры) и `int SupplyCapacity`. Валидация: если тип задан, ёмкость > 0.
- `ServicePoint.Supply` — `SupplyStock` или `null`, создаётся в ctor полным (GDD §8: старт с полным запасом).
- `TickAccept`: если `Supply != null && Supply.IsEmpty` → ждать, таймер приёма **не** растёт. При принятии → `Supply.TryConsume()` до `OrderAccepted`.
- `bool IsWaitingForSupply` = `State == AwaitingAccept && Supply != null && Supply.IsEmpty` (для HUD).
- Апгрейды: `float DurationMultiplier` (по умолчанию 1) и `double PriceMultiplier` (по умолчанию 1), сеттер `ApplyModifiers(float durationMultiplier, double priceMultiplier)` с валидацией (> 0, не NaN). `TickService` использует `ServiceDuration * DurationMultiplier`. `AcceptDelay` апгрейд не трогает.
- `LocationTraffic.PriceFor` и плата на въезде (`PriceFormula.TimeBased(...)`) умножают результат на `point.PriceMultiplier`. **Why:** цена фиксируется при резерве, поэтому апгрейд действует только на новые заказы. Это честно и не требует пересчёта.
- Обновить существующие тесты `ServicePoint` и трафика под новые параметры definition.

### 3.3 `Domain/Upgrades/`
- `UpgradeKind`: `Speed`, `Price`.
- `UpgradeTrack` (сущность): `int Level`, `int MaxLevel`, `bool IsMaxed`, `void LevelUp()` (на max → `InvalidOperationException`), `void Restore(int level)` (clamp).
- Формулы — в `UpgradeSettings` (Services), см. §5.

### 3.4 `Domain/Staff/`
- `StaffRole`: `PointWorker`, `Storekeeper`.
- `StaffMember` (сущность): `int Id`, `StaffRole Role`, `string LocationId`, `string AssignedPointId` (для работника), `StaffState State`.
- `StaffState`: `WalkingToSpot`, `WaitingForSpot` (WorkSpot занят игроком), `Working` (работник); `Idle`, `ToWarehouse`, `WaitingForMoney`, `ToPoint` (кладовщик). Переходы — методами сущности. Недопустимый переход → `InvalidOperationException`. Сущность не знает про NavMesh.
- `StaffMember.CarriedBox` (`SupplyBox`), `TargetPointId` (кладовщик).

## 4. Services

### 4.1 `Services/Supplies/`
```csharp
public interface ISupplyService
{
    /// Point of the location with the lowest Fill01 among points whose stock can take a whole box,
    /// counting boxes already on their way (storekeeper targets). Null when none.
    ServicePoint FindHungriest(string locationId);
    Money GetBoxPrice(string supplyTypeId);
    /// Charges the wallet and returns a box for FindHungriest's supply type. False: nothing to restock / no money.
    bool TryBuyBoxForHungriest(string locationId, out SupplyBox box, out ServicePoint target);
    bool CanDeliver(in SupplyBox box, string pointId);
    bool TryDeliver(in SupplyBox box, string pointId);
    void MarkIncoming(string pointId, int units);  // storekeeper reservation
    void ClearIncoming(string pointId, int units);
}
```
- `SupplyService` (ctor: `IServicePointService`, `IWalletService`, `IConfigProvider`, `IEventBus`). Публикует `BoxBoughtEvent { LocationId; SupplyTypeId; Price; Carrier: StaffRole? → bool ByPlayer }`, `SupplyDeliveredEvent { PointId; Units; ByPlayer }`, `SupplyDepletedEvent { PointId }` (запас стал 0; нужно онбордингу и облачкам).
- `PlayerCarry` (`IPlayerCarry`): `bool HasBox`, `SupplyBox Box`, `bool TryPick(SupplyBox)`, `SupplyBox Drop()`, `event Action Changed`. Состояние «в руках» живёт в Services, Presentation только показывает его. **Не сохраняется** (TDD §4.15: персонаж стартует пустым).
- **Почему target при покупке не фиксируется за игроком:** игрок может отнести ящик на любую точку того же типа. Входящим (`MarkIncoming`) считается только ящик кладовщика.

### 4.2 `Services/Upgrades/`
```csharp
public interface IUpgradeService
{
    int GetLevel(string pointId, UpgradeKind kind);
    Money GetNextCost(string pointId, UpgradeKind kind);
    UpgradeAvailability GetAvailability(string pointId, UpgradeKind kind); // Maxed | Locked | NotEnoughMoney | Available
    bool TryUpgrade(string pointId, UpgradeKind kind);
    event Action<string, UpgradeKind> Upgraded;
    // save (module B)
    void Restore(string pointId, UpgradeKind kind, int level);
}
```
- `UpgradeService` регистрирует треки при `Register(ServicePoint)`. Точки, построенные в A1, регистрируются из `BuildableBinder` / инсталлера. После каждого изменения → `point.ApplyModifiers(...)`. Публикует `UpgradePurchasedEvent { PointId; Kind; NewLevel; Cost }`.
- Формулы (`UpgradeSettings`): стоимость = `PriceFormula.Evaluate(baseCost, growth, level)`. Speed: `durationMultiplier = pow(1 - 0.10, level)` (на ур. 10 ≈ 0.35, никогда не 0). Price: `priceMultiplier = 1 + 0.15 × level`.

### 4.3 `Services/Staff/`
```csharp
public interface IStaffService
{
    HireAvailability GetWorkerAvailability(string pointId);           // Hired | Locked | NotEnoughMoney | Available | NotSupported (no worker config)
    Money GetWorkerCost(string pointId);
    bool TryHireWorker(string pointId);
    bool HasWorker(string pointId);                                    // true from the moment of hire (also while walking)
    HireAvailability GetStorekeeperAvailability(string locationId);
    Money GetStorekeeperCost(string locationId);
    bool TryHireStorekeeper(string locationId);
    event Action<StaffMember> Hired;
    // save (module B): spawn without paying
    void RestoreWorker(string pointId);
    void RestoreStorekeeper(string locationId);
    IReadOnlyList<StaffMember> Staff { get; }
}
```
- Порт **`IStaffAgents`** (Services, реализует Presentation — по аналогии с `ICarAgents`):
  ```csharp
  void Spawn(int staffId, StaffRole role, string locationId);   // in the location's staff room
  void MoveTo(int staffId, StaffDestination destination);       // WorkSpot(pointId) | Warehouse(locationId) | SupplyDrop(pointId)
  void SetCarried(int staffId, string supplyTypeIdOrEmpty);
  event Action<int> Arrived;
  ```
- `StaffService : ITickable, IDisposable` (ctor: `IServicePointService`, `ISupplyService`, `IWalletService`, `IUnlockGate`, `IStaffAgents`, `IConfigProvider`, `IEventBus`):
  - **Работник:** найм → списать $ → `Spawn` → `MoveTo(WorkSpot)` → `Arrived` → `TryOccupy(Worker)`; если не вышло (на месте игрок) → `WaitingForSpot`, повторять в `Tick` до успеха. **Why:** не выталкиваем игрока принудительно, чтобы не рассинхронизировать FSM персонажа. После найма `ServicePointView.IsInteractable` = false (`HasWorker`), поэтому игрок не займёт место снова, когда уйдёт.
  - **Кладовщик:** `Idle` → (в `Tick`, без аллокаций) `FindHungriest` с порогом `Fill01 < StorekeeperRestockThreshold` (0.5) → `MoveTo(Warehouse)` → `Arrived` → `TryBuyBox…`. Нет денег → `WaitingForMoney`, повтор раз в 1 с. При покупке → `MarkIncoming` → `SetCarried` → `MoveTo(SupplyDrop)` → `Arrived` → `TryDeliver`. Если не вышло (место кончилось), отдать ящик в другую подходящую точку, иначе выбросить. → `ClearIncoming` → `Idle`.
  - Публикует `StaffHiredEvent { Role; LocationId; PointId }`.
- **Ассиметрия, которую надо сохранить (GDD 2026-09-30):** игрок не помогает работнику, работник не носит ящики.

### 4.4 `Services/Points/PointIncomeTracker` (`ITickable`)
Слушает `OrderAcceptedEvent`. По каждой точке держит кольцевой буфер **6 корзин по 10 с** (массивы создаются при регистрации точки). `Money GetIncomePerMinute(string pointId)` = сумма корзин. Время берётся из `Tick(dt)`, на паузе стоит. Ноль аллокаций в тике.

### 4.5 Конфиги (Infrastructure SO → Services settings)
- `SupplyTypeConfig` (`AutoService/Supply Type`): `_id`, `_displayName`, `_boxPrice`, `_unitsPerBox` (5). Активы: `SUP_Shampoo` ($5), `SUP_Oil` ($15), `SUP_Tires` ($25).
- `ServiceTypeConfig` + поля: `_supplyTypeId` (пусто у `parking`), `_supplyCapacity` (10), `_workerTitle` («Parking Attendant», «Washer», «Oil Mechanic», «Tire Mechanic»), `_workerHireCost` ($150 / $300 / $400 / $700), `_workerRequiredLevel` (1 / 2 / 3 / 4).
- `StaffSection` в `GameConfig`: `_storekeeperTitle` («Storekeeper»), `_storekeeperDescription`, `_storekeeperCost` ($600), `_storekeeperRequiredLevel` (3), `_restockThreshold` (0.5).
- `UpgradeConfig` (`AutoService/Upgrade`): `_kind`, `_displayName` («Speed» / «Price»), `_effectFormat` («−10% service time» / «+15% price»), `_baseCost` (100), `_growth` (1.35), `_maxLevel` (10), `_effectPerLevel` (0.10 / 0.15).
- Все строки UI — из конфигов. Шаблоны вида «Lv {0}», «Hire {0}», «Need {0}», «{0}/min» — `[SerializeField]` на View (префаб), не в коде презентера.

## 5. Presentation

### 5.1 Площадки
- `WorkPadMarker` — без кода: builder кладёт жёлтый плоский квад (`M_WorkPad`) под `ApproachPoint` каждой точки. Коллайдер не нужен: кликается сама точка, как сейчас.
- **`ManagePadView : MonoBehaviour, IInteractable`**: `[SerializeField] string _targetId` (pointId или locationId склада), `ManagePadTarget _target` (`ServicePoint` | `Warehouse`), `Transform _approachPoint`, `InteractableHighlight`, `DwellRingView _ring`, `float _dwellSeconds = 1.5f`, `Transform _panelAnchor`. Визуал — синий диск (`M_ManagePad`) + world-space иконка ⬆ (TMP-символ или built-in sprite). События `DwellCompleted`, `Left`. Механика dwell — `DwellProgress` из A1, повторяем приём A1 (`BuildPlotView`).
- Пад недостроенной точки (A1) **неактивен** до постройки: `BuildableBinder` включает его вместе с `_target`. Проще всего, если пад — ребёнок `_target`.

### 5.2 Панель точки — `Presentation/Points/Panel/`
- **`PointPanelView`** (screen-space, один на сцену):
  - `_title`, `_status` («Supply 7/10 · Washer: hired»), `_income` («$84/min»);
  - два `UpgradeRowView` (`_name`, `_level`, `_effect`, `Button _buy`, `_buyLabel`);
  - `HireRowView` (`_title`, `Button _hire`, `_hireLabel`, скрыт у точек без конфига работника);
  - `_closeButton`.
  - События `UpgradeClicked(UpgradeKind)`, `HireClicked`, `CloseClicked`.
- **`PointPanelPresenter : ITickable, IDisposable`**:
  - тикает dwell пада, на котором стоит игрок → открывает панель → заполняет её;
  - обновляет по событиям (`BalanceChanged`, `Upgraded`, `Hired`, `SupplyStock.Changed`), не каждый кадр, кроме позиции;
  - доход обновлять раз в 1 с;
  - закрытие — как в A1 (`Left`, Close, Esc, точка за камерой).
- **Позиционирование «рядом с объектом, в границах экрана»** сейчас в `BuildPanelView.SetScreenPosition` + `SetOnScreen` + `ActivateChainToRoot`. Вынести в общий компонент `ScreenAnchoredPanel : MonoBehaviour` (на корне любой такой панели: `_root`, offset, margin; `Show()`/`Hide()` с включением цепочки объектов, `Follow(Camera, Vector3 worldAnchor)`). `BuildPanelView` / `OfferPanelView` / `PointPanelView` используют его.
- Тексты кнопок: `Upgrade $135` / `Need $135` / `MAX`; `Hire Washer $300` / `Need $300` / `Hired` / `Locked (Lv 2)`.
- Все числа выводить через `TMP_Text.SetText("{0}", …)` или кешированный `MoneyFormatter`, без конкатенации в тике.

### 5.3 Склад — `Presentation/Supplies/`
- **`WarehouseView : MonoBehaviour, IInteractable`** (`_locationId`, `_approachPoint`, highlight). `BeginInteraction`: если руки пусты → `ISupplyService.TryBuyBoxForHungriest` → `IPlayerCarry.TryPick`. Неудачу показать коротким world-space текстом над складом: «Hands full» / «All stocked» / «Need $15». Строки — `[SerializeField]`; показ на 1.5 с, unscaled.
- ManagePad склада → панель-«оффер» («Storekeeper · Carries boxes to the hungriest bay · Hire $600»). `BuildPanelView` по сути и есть оффер (title / description / cost / requirement / кнопка) → **переименовать в `OfferPanelView`** (событие `ActionClicked` вместо `BuildClicked`, форматы подписей остаются полями). Переименование класса ломает ссылку на скрипт в сцене → сохранить GUID: переименовать файл и класс, **не трогая `.meta`** (GUID в `.meta` тот же — Unity найдёт скрипт). Второй экземпляр панели создаёт setup-утилита (§8).
- `ServicePointView.BeginInteraction`: сначала, если в руках ящик подходящего типа и `CanDeliver` → `TryDeliver` + `Drop`, затем занять место. Неподходящий тип: ящик остаётся в руках, игрок работает. Зависимости `ISupplyService`, `IPlayerCarry` → `Construct(...)`.
- **`PlayerCarryView`** (на `PlayerView`): `_boxSocket`, `_boxRenderer` (куб 0.5³). Цвет — из `SupplyVisualCatalog` (SO в Presentation: supplyTypeId → Color + иконка). Подписка на `IPlayerCarry.Changed`.
- `ServicePointHud` + `_supplyLabel` (TMP, «7/10», обновлять только при `Changed`), `_noSupplyIcon` (показывать при `Supply.IsEmpty`; «!» ожидания приёма при этом скрыть — сейчас нужен ящик, а не исполнитель).

### 5.4 NPC — `Presentation/Staff/`
- **`StaffView`** (префаб): капсула + «голова», `NavMeshAgent` (humanoid), `_carrySocket` + `_boxRenderer`, `Renderer _body`. Цвет по роли — из `StaffVisualCatalog` (role → prefab или material).
- **`StaffAgents : IStaffAgents, ITickable, IDisposable`** (по аналогии с `CarAgents`):
  - `Instantiate` при найме (T6: без пула) в `LocationLayout.StaffRoom`;
  - destination → `Transform`: WorkSpot = `ServicePointView.ApproachPoint`, Warehouse = `WarehouseView.ApproachPoint`, SupplyDrop = `ManagePadView.ApproachPoint` точки;
  - проверка прибытия в `Tick` (как у `PlayerMotor`), поворот к цели, `Arrived(id)`.
- `ServicePointView.IsInteractable` = `!staff.HasWorker(pointId)` (вместо проверки `Occupant != Worker`).

### 5.5 `LocationLayout` (+ поля)
`_warehouse` (`WarehouseView`), `_warehousePad` (`ManagePadView`), `_staffRoom` (`Transform`), `_managePads` (`ManagePadView[]`, по одному на точку, включая оба шлагбаума). В `Validate`:
- у каждой точки ровно один пад;
- `_targetId` пада указывает на существующую точку;
- склад и комната персонала заданы.

## 6. Интеграция
`StaffSuppliesInstaller` (после `BuildingInstaller`, до `HudInstaller`):
1. `PlayerCarry`, `SupplyService`, `UpgradeService`, `PointIncomeTracker`, `StaffAgents`, `StaffService`, `PointPanelPresenter`, презентер панели склада, `PlayerCarryView.Construct`.
2. Регистрация стартовых точек в `UpgradeService` / `PointIncomeTracker` / `ServicePointView.Construct(...)`.
3. Построенные точки регистрируются тем же `PointRegistrar` (см. §2), который `BuildableBinder` уже вызывает при `Built` / `BuiltRestored`.

Порядок тиков: point service → traffic → staff service → car agents → staff agents → income tracker → presenters.

## 7. Тесты (EditMode)
- `SupplyStockTests`: consume / add / CanAdd / переполнение → исключение / Restore clamp.
- `ServicePointTests`: пустой запас блокирует приём (таймер не растёт); приём тратит 1; `DurationMultiplier` 0.5 → вдвое быстрее; барьер без запаса работает как раньше.
- `SupplyServiceTests`: hungriest с учётом incoming; полная точка не выбирается; покупка списывает цену; нет денег → false, без списания; деливери неподходящего типа → false.
- `UpgradeServiceTests`: стоимость по формуле; max → `Maxed`; `Locked` при фейковом gate; модификаторы применены к точке; `Restore`.
- `StaffServiceTests` (фейковый `IStaffAgents`): найм → spawn + MoveTo(WorkSpot) → Arrived → Occupant=Worker; игрок на месте → `WaitingForSpot`, место занимается после ухода игрока; кладовщик: голодная точка → склад → покупка → доставка → Idle; нет денег → ждёт, потом продолжает; повторный найм → false.
- `PointIncomeTrackerTests`: сумма за окно; через 60+ с старое выпадает.
- `LocationTrafficTests`: цена заказа учитывает `PriceMultiplier`.

## 8. Editor-инструменты
- `Create Location 1 Configs` → дополнить: SupplyType ×3, поля supply/worker у `ST_*`, `StaffSection`, `UpgradeConfig` ×2 (`UPG_Speed`, `UPG_Price`). Существующие ассеты **дозаполнять** пустые поля, не перезаписывать.
- `Build Location 1 Roads` → дополнительно (под маркером `WhiteboxGenerated`):
  - **Склад** `Warehouse_1`: куб 4×2.5×3 в **(4,0,-6)**, слой Interactable, `WarehouseView`, ApproachPoint с западной стороны; ManagePad склада — с южной стороны.
  - **Комната персонала** `StaffRoom_1`: куб 3×2.5×3 в **(4,0,2)** + `Door` Transform (точка спавна) с западной стороны.
  - Обе постройки — на островке между сервисной мойкой 1 (x=-3), парковкой (x≥9), основной дорогой и верхней дорогой: короткие пробежки до обоих шлагбаумов и мойки. Кодер проверяет по фактической разметке v3.1, что островок свободен, и при пересечениях сообщает в PR.
  - **WorkPad** под ApproachPoint каждой точки (квад 1.2×1.2, `M_WorkPad` жёлтый); **ManagePad** — диск ⌀1.2 (`M_ManagePad` синий) в ≈2 м от WorkSpot вдоль внешней стены бокса / рядом с будкой шлагбаума, **не на полосе машин и не на пути NPC к WorkSpot**; ApproachPoint пада лицом к точке; Canvas с ⬆ и `DwellRingView`.
  - `M_Ghost` → голубоватый (0.55, 0.75, 1, α 0.35).
  - **K4:** у каждого бокса (`_target` участков и мойки 1) — `NavMeshObstacle` (Box по стенам, **Carve** on, Carve Only Stationary). Так персонаж и NPC не проходят сквозь стены построенного бокса, перепекать не нужно. Не перекрывать полосу машин и WorkSpot.
- **`AutoService/Setup/Run A2 Setup`** (новый, `Bootstrap/Editor/ModuleSetupA2.cs`) — пользователь жмёт одну кнопку:
  1. `Create Location 1 Configs` (дополненный);
  2. `Build Location 1 Roads`;
  3. создать/обновить префаб `Assets/_Project/Prefabs/Staff.prefab` (`PrefabUtility.SaveAsPrefabAsset`): Capsule h 1.8 + `NavMeshAgent` (humanoid, speed 3.5) + `StaffView` на корне, дочерний `CarrySocket` (0, 1.1, 0.5) с кубом 0.5 без коллайдера;
  4. создать `StaffVisualCatalog` (PointWorker → синий материал, Storekeeper → оранжевый) и `SupplyVisualCatalog` (shampoo — голубой, oil — тёмно-жёлтый, tires — тёмно-серый), материалы — в `Materials/`;
  5. на `Player` — `PlayerCarryView` + `CarrySocket` с кубом;
  6. в `ScreenHud` — `PointPanel` и `OfferPanel` (склад) по раскладке §9 (размеры, anchor/pivot, цвета, шрифт TMP по умолчанию), все ссылки через `SerializedObject`;
  7. на `ServicePointHud` каждой точки — `SupplyLabel` и `NoSupplyIcon`;
  8. назначить всё новое в `[EntryPoint]` и `LocationLayout`;
  9. Bake всех `NavMeshSurface`, валидация `LocationLayout`, сохранение сцены и ассетов, итоговый лог.
  Повторный запуск идемпотентен (маркер `WhiteboxGenerated`). Ручных шагов в Editor setup быть не должно.

## 9. Editor setup (пользователь) — в PR
1. **AutoService → Setup → Run A2 Setup** → в Console итог без ошибок.
2. Play-сценарий:
   - мыть машины, пока у мойки не станет 0/10 → машина стоит, над мойкой иконка ящика;
   - клик по складу → в руках голубой ящик, −$5 → клик по мойке → 5/10, работа продолжается;
   - синий пад у шлагбаума 1 → постоять 1.5 с → панель → **Hire Parking Attendant $150** → NPC выходит из комнаты, встаёт на WorkSpot, шлагбаум работает без игрока; жёлтая площадка больше не кликается;
   - в панели мойки **Speed** → время мойки меньше, следующий уровень дороже; **Price** → следующий заказ дороже;
   - синий пад склада → **Hire Storekeeper $600** → кладовщик сам носит ящики к самой пустой точке; без денег ждёт;
   - доход/мин в панели растёт, пока точка работает;
   - сквозь стены построенного бокса пройти нельзя (K4).

**Раскладка панелей для setup-утилиты** (канвас 1920×1080; стиль как у панели стройки: фон `1E2430` α 235, заголовок 26 Bold белый, текст 18 `C8CED8`, цены 22 Bold `FFD54F`, кнопки `43A047` / disabled `5A5F66`, Close 32×32 в правом верхнем углу):
- `PointPanel` 380×300, pivot (0,0): Title (top, −12, h 34), Status (−50, h 24), Income (−76, h 24), UpgradeSpeed (−108, h 52: Name + Level слева, Effect под ними, кнопка 140×40 справа), UpgradePrice (−166, h 52, так же), Hire (снизу 16, h 44: кнопка на всю ширину).
- `OfferPanel` — копия раскладки панели стройки 360×220.

## 10. Критерии приёмки
- [ ] R1: поведение не изменилось, сцена не требует переназначений.
- [ ] Тесты зелёные; Domain/Services без Unity; ноль аллокаций в тиках (в том числе у кладовщика и трекера дохода).
- [ ] Новый тип расходника / работника / апгрейда добавляется только данными.
- [ ] Сценарий §9.7 проходит; игрок и работник никогда не стоят на одном WorkSpot.
- [ ] События для онбординга (модуль 10) публикуются: `BoxBoughtEvent`, `SupplyDeliveredEvent`, `SupplyDepletedEvent`, `StaffHiredEvent`, `UpgradePurchasedEvent`.

## 11. Git
Коммиты:
1. `Split GameplayEntryPoint into module installers`
2. `Add supply stock and point modifiers to domain`
3. `Add supply, upgrade and staff services`
4. `Add income tracker`
5. `Add staff, supply and upgrade tests`
6. `Add supply, staff and upgrade configs`
7. `Add manage pads and point panel`
8. `Add warehouse, carry and staff views`
9. `Wire staff and supplies installer`
10. `Extend config creator and layout builder for A2`

PR `05: Staff, supplies, upgrades (A2)` + Editor setup.
