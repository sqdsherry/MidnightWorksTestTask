# Промпт 03 — Service loop: точки, очередь, парковка, трафик машин

**Ветка:** `feature/03-service-loop` (от актуального `main`) · **PR:** в `main`
**Перед началом:** прочитай `CLAUDE.md`, `Docs/GDD.md` §3, §4.2, §5, `Docs/TDD.md` §4.3–4.5.

---

## 1. Контекст и цель

Смержены 01 (Core) и 02 (Player: click-to-move, `IInteractable`, камера).

Этот модуль — **сердце игры**, первый играбельный цикл на whitebox:

```
дорога ══► [очередь-полоса: N слотов] ══► развилка ─┬─► точка услуги (мойка)
                                                    └─► [шлагбаум] ─► парковка ─► точка услуги
```

1. Машина спавнится на дороге и встаёт в хвост очереди-полосы (полна → машина не спавнится).
2. **Голова очереди** у развилки: есть **свободная** точка нужной услуги и **никто с парковки её не ждёт** → едет прямо на неё; иначе, если свободны шлагбаум и место на парковке → едет к шлагбауму; иначе ждёт.
3. **Шлагбаум** — это тоже точка (`PointKind.Barrier`): машина ждёт, пока на рабочем месте шлагбаума кто-то стоит → заказ принят → **+$ за парковку** → короткое «обслуживание» (шлагбаум поднимается) → машина едет на зарезервированное место парковки.
4. **Диспетчер** отправляет машины с парковки (FIFO по времени парковки) на освободившиеся точки нужной услуги.
5. **Точка услуги**: машина подъехала → ждёт приёма. Заказ принимается **только пока на рабочем месте кто-то есть** (игрок; работники — модуль 06) → **+$ за услугу** → прогресс обслуживания идёт **только пока рабочее место занято** (ушёл — пауза) → готово → машина уезжает к выезду и исчезает (пул), публикуется событие «обслужено» (для XP в модуле 07).
6. Игрок занимает рабочее место, кликнув по точке (`IInteractable.BeginInteraction` из модуля 02), освобождает — уйдя.

**Вне модуля:** расходники (04), строительство (05 — сейчас все точки на сцене считаются построенными), работники (06), уход злых клиентов (14 — терпение здесь только считаем), полноценный HUD (09; здесь — минимальный текст баланса).

## 2. Правила

Всё из `CLAUDE.md`. Особенно:
- **Domain и Services — без Unity.** Машины, точки, очередь, парковка, диспетчер — чистый C# и покрыты EditMode-тестами. Unity-часть (NavMesh, трансформы) — только в Presentation, общается с Services через интерфейс `ICarAgents` и id.
- Id точек/услуг/типов машин — **строки из конфигов/сцены**; id машин — рантайм `int`.
- Ноль аллокаций в `Tick` (списки/массивы заранее, без LINQ/замыканий; `string` id не конкатенировать в тике).
- Пул машин — `UnityEngine.Pool.ObjectPool<CarView>`.
- Мини-правка из ревью 02: в `InteractableHighlight` значение по умолчанию `_property` → `HighlightProperty.BaseColorTint` (работает с любым материалом).

## 3. Domain — `AutoService.Domain.*`

### 3.1 `Domain/Points/`

```csharp
public enum PointKind { Barrier = 0, Service = 1 }
public enum OccupantKind { None = 0, Player = 1, Worker = 2 }
public enum ServicePointState
{
    Idle = 0,            // свободна, машина не назначена
    Reserved = 1,        // машина назначена и едет
    AwaitingAccept = 2,  // машина на месте, ждёт приёма
    Servicing = 3,       // заказ принят, идёт обслуживание
    Clearing = 4,        // готово, машина отъезжает; точка недоступна ClearDelay секунд
}
```

**`ServicePointDefinition`** — неизменяемые данные точки (собираются из конфига услуги + сцены):
`string Id`, `string LocationId`, `string ServiceTypeId`, `PointKind Kind`, `Money BasePrice`, `float ServiceDuration` (с), `float AcceptDelay` (с, «приём» после прихода исполнителя, для ощущения), `float ClearDelay` (с). Валидация в ctor (пустые id, отрицательные времена → `ArgumentException`).

**`ServicePoint`** — сущность:
```csharp
public ServicePoint(ServicePointDefinition definition);
public ServicePointDefinition Definition { get; }
public ServicePointState State { get; }
public OccupantKind Occupant { get; }
public bool IsOccupied { get; }
public int CarId { get; }                 // -1 если нет
public float Progress { get; }             // 0..1 обслуживания
public Money CurrentPrice { get; }         // цена текущего заказа
public bool IsAvailable { get; }           // State == Idle

public event Action<ServicePoint> StateChanged;
public event Action<ServicePoint> OccupantChanged;
public event Action<ServicePoint, Money> OrderAccepted;   // деньги начисляет Services
public event Action<ServicePoint, int> ServiceCompleted;  // carId

public bool TryOccupy(OccupantKind occupant);   // false, если уже занято другим (None → ArgumentException)
public void Vacate(OccupantKind occupant);      // освобождает, только если занято этим же occupant
public bool TryReserve(int carId, Money price); // только из Idle
public void NotifyCarArrived(int carId);        // Reserved → AwaitingAccept (чужой carId → InvalidOperationException)
public void Tick(float deltaTime);
```
`Tick`: `AwaitingAccept` + занято → копит таймер `AcceptDelay` → `Servicing` + `OrderAccepted(price)`; таймер сбрасывается, если исполнитель ушёл до приёма. `Servicing` + занято → `Progress += dt / ServiceDuration` → 1 → `Clearing` + `ServiceCompleted(carId)`, `CarId = -1`. `Clearing` → через `ClearDelay` → `Idle`. Без исполнителя `AwaitingAccept`/`Servicing` стоят на месте.

### 3.2 `Domain/Traffic/`

**`CarType`** — неизменяемые данные: `string Id`, `int SpawnWeight` (>0), `double PriceMultiplier` (>0), `float Patience` (с).

**`CarState`**: `Arriving, InQueue, ToBarrier, AtBarrier, ToParking, Parked, ToPoint, AtPoint, Leaving`.

**`Car`** — сущность:
```csharp
public Car(int id, CarType type, string requestedServiceTypeId);
public int Id { get; }  public CarType Type { get; }  public string RequestedServiceTypeId { get; }
public CarState State { get; }
public bool HasArrived { get; }            // доехал до текущей цели
public float PatienceLeft { get; }  public float Patience01 { get; }
public int ParkingSlot { get; }            // -1 если нет
public string TargetPointId { get; }       // null если нет
public float ParkedAtTime { get; }         // для FIFO диспетчера
// Переходы (каждый проверяет допустимое исходное состояние, иначе InvalidOperationException):
public void EnterQueue();                  // Arriving → InQueue (HasArrived = false)
public void SendToBarrier(int parkingSlot);
public void SendToParking();               // AtBarrier → ToParking
public void SendToPoint(string pointId);   // InQueue | Parked → ToPoint
public void Leave();                       // AtPoint → Leaving
public void MarkArrived(float time);       // HasArrived = true; ToBarrier→AtBarrier, ToParking→Parked(ParkedAtTime=time), ToPoint→AtPoint
public void TickPatience(float deltaTime); // убывает в InQueue/AtBarrier/Parked/AtPoint (пока не начато обслуживание — флаг ставит Services); не ниже 0
public event Action<Car> PatienceDepleted; // один раз; реакция — модуль 14
```
> Терпение на `AtPoint` убывает только до начала обслуживания: Services вызывает `TickPatience` лишь пока точка в `AwaitingAccept`.

**`EntryQueue`** — очередь-полоса фиксированной ёмкости:
```csharp
public EntryQueue(int capacity);
public int Capacity { get; }  public int Count { get; }  public bool IsFull { get; }
public bool TryEnqueue(int carId, out int slotIndex);
public int Head { get; }                    // carId или -1
public int SlotOf(int carId);               // -1 если нет
public int GetAt(int slotIndex);            // carId или -1
public void RemoveHead();                   // остальные сдвигаются на слот вперёд
public event Action<EntryQueue> Shifted;    // после RemoveHead — подписчик переназначит цели машин
```

**`ParkingLot`**:
```csharp
public ParkingLot(int capacity);
public int Capacity { get; }  public int FreeCount { get; }
public bool TryReserve(int carId, out int slotIndex);
public void Release(int slotIndex);
public int CarAt(int slotIndex);
public void SetCapacity(int capacity);      // задел под модуль 05: только увеличение
```

## 4. Services — `AutoService.Services.*`

### 4.1 Конфиг-модели (`Services/Config/`)

Расширь `IConfigProvider`:
```csharp
IReadOnlyList<ServiceTypeSettings> ServiceTypes { get; }
IReadOnlyList<CarType> CarTypes { get; }
TrafficSettings Traffic { get; }
bool TryGetServiceType(string id, out ServiceTypeSettings settings);
```
- `ServiceTypeSettings`: `Id`, `DisplayName`, `PointKind Kind`, `Money BasePrice`, `float ServiceDuration`, `float AcceptDelay`, `float ClearDelay`.
- `TrafficSettings`: `float SpawnInterval`, `float SpawnIntervalJitter` (± с), `int MaxCarsAlive`.

### 4.2 Точки — `Services/Points/`

```csharp
public interface IServicePointService
{
    bool TryGet(string pointId, out ServicePoint point);
    IReadOnlyList<ServicePoint> All { get; }
    bool TryOccupy(string pointId, OccupantKind occupant);
    void Vacate(string pointId, OccupantKind occupant);
    /// Free (Idle) service point of the given type in the location, or null. First by registration order.
    ServicePoint FindAvailable(string locationId, string serviceTypeId);
}
```
`ServicePointService : IServicePointService, ITickable, IDisposable` (ctor: `IWalletService`, `IEventBus`):
- `Register(ServicePointDefinition)` — вызывает EntryPoint при сборке сцены; дубль id → исключение.
- Тикает все точки; подписан на их события: `OrderAccepted` → `wallet.Add(price)` + `OrderAcceptedEvent`; `ServiceCompleted` → `ServiceCompletedEvent`.

События шины (`readonly struct`, `Services/Points/`):
- `OrderAcceptedEvent { string PointId; string ServiceTypeId; PointKind Kind; Money Price; }` — для «+$» и звука.
- `ServiceCompletedEvent { string PointId; string ServiceTypeId; PointKind Kind; int CarId; string CarTypeId; }` — для XP (модуль 07). `CarTypeId` заполняет `LocationTraffic` → поэтому событие публикует **он**, а не `ServicePointService` (реши аккуратно: один источник события; задокументируй выбор).

### 4.3 Трафик — `Services/Traffic/`

**`CarDestination`** — `readonly struct`: `CarDestinationKind Kind` (`QueueSlot, Barrier, ParkingSlot, Point, Exit`), `int Index` (для слотов), `string PointId` (для точек). Фабрики `QueueSlot(i)`, `Barrier()`, `ParkingSlot(i)`, `Point(id)`, `Exit()`.

**`ICarAgents`** — порт к физическим машинам, **реализуется в Presentation**:
```csharp
public interface ICarAgents
{
    /// Spawns a car visual at the location's spawn point.
    void Spawn(int carId, string carTypeId);
    /// Drives the car to the destination; raises Arrived once it is there and aligned.
    void MoveTo(int carId, CarDestination destination);
    /// Returns the car visual to the pool.
    void Despawn(int carId);
    event Action<int> Arrived;
}
```

**`LocationTraffic : ITickable, IDisposable`** — оркестратор одной локации (2-я локация = второй экземпляр).
ctor: `LocationTrafficDefinition` (`LocationId`, `BarrierPointId`, `QueueCapacity`, `ParkingCapacity`), `IServicePointService`, `ICarAgents`, `IConfigProvider`, `IRandom`, `IEventBus`.
- Внутри: `EntryQueue`, `ParkingLot`, `Dictionary<int, Car>` + предвыделенный список активных машин для итерации без аллокаций, счётчик id, таймер спавна, игровое время (сумма dt) для `ParkedAtTime`.
- **Спавн**: по таймеру (`SpawnInterval ± Jitter` через `IRandom`), если очередь не полна и живых < `MaxCarsAlive`. Тип машины — взвешенный рандом по `SpawnWeight`. Услуга — случайная из **типов точек `Service` этой локации**, зарегистрированных в `IServicePointService` (нет ни одной → не спавнить). `Spawn` → `EnterQueue` → `MoveTo(QueueSlot(slot))`. Публикуй `CarSpawnedEvent`.
- **`Shifted`** очереди → каждой машине в очереди `MoveTo(QueueSlot(новый слот))`, `HasArrived = false`.
- **Голова очереди** (в `Tick`, если доехала до слота 0):
  1. если точка нужной услуги свободна **и** среди припаркованных нет машины, ждущей эту услугу → `TryReserve(point)`, `RemoveHead`, `SendToPoint`, `MoveTo(Point)`;
  2. иначе если шлагбаум `Idle` и есть место на парковке → зарезервировать место и шлагбаум (`TryReserve(carId, parkingFee)`), `RemoveHead`, `SendToBarrier(slot)`, `MoveTo(Barrier)`;
  3. иначе ждать.
- **Диспетчер парковки**: каждая припаркованная машина в порядке `ParkedAtTime` → свободна точка её услуги → reserve, `parking.Release`, `SendToPoint`, `MoveTo(Point)`.
- **Цена**: `BasePrice × CarType.PriceMultiplier` (`Money * double`). Для шлагбаума — `BasePrice` услуги шлагбаума × множитель тоже (единообразно).
- **Прибытия** (`ICarAgents.Arrived`): `car.MarkArrived(time)`; `AtBarrier` / `AtPoint` → `point.NotifyCarArrived(carId)`.
- **Завершения** (подписка на `ServicePoint.ServiceCompleted` точек своей локации): шлагбаум → `SendToParking`, `MoveTo(ParkingSlot(car.ParkingSlot))`; точка услуги → `car.Leave()`, `MoveTo(Exit)`, публикует `ServiceCompletedEvent`.
- **Выезд**: прибытие в `Leaving` → `Despawn`, удалить машину, `CarLeftEvent { CarId; CarLeaveReason Reason }` (`Served`; `Angry` — задел под 14).
- **Терпение**: `TickPatience` для машин в `InQueue/AtBarrier/Parked`, и `AtPoint` пока точка `AwaitingAccept`.
- Публичное read-only состояние для UI/отладки: `QueueCount`, `ParkingFree`, `CarsAlive`, `bool TryGetCar(int, out Car)`.

## 5. Infrastructure — конфиги

- `ServiceTypeConfig : ScriptableObject` (`[CreateAssetMenu("AutoService/Service Type")]`): `_id`, `_displayName`, `_kind`, `_basePrice` (long), `_serviceDuration`, `_acceptDelay = 0.4`, `_clearDelay = 1.5`; `OnValidate` — clamp.
- `CarTypeConfig : ScriptableObject` (`"AutoService/Car Type"`): `_id`, `_spawnWeight`, `_priceMultiplier`, `_patience`.
- `TrafficSection` (`[Serializable]` в `GameConfig`): `_spawnInterval = 7`, `_spawnIntervalJitter = 2`, `_maxCarsAlive = 12`.
- `GameConfig`: `[SerializeField] ServiceTypeConfig[] _serviceTypes`, `CarTypeConfig[] _carTypes`, `TrafficSection _traffic`.
- `ScriptableObjectConfigProvider` маппит всё один раз; дубли/пустые id, null-элементы → понятная ошибка (исключение с именем ассета).

## 6. Presentation — `AutoService.Presentation.*`

### 6.1 `Presentation/Points/`

**`ServicePointView : MonoBehaviour, IInteractable`** — точка на сцене (мойка, шлагбаум, …).
- `[SerializeField]`: `string _pointId`, `string _serviceTypeId`, `Transform _carSpot` (куда встаёт машина, её forward = направление машины), `Transform _approachPoint` (рабочее место исполнителя), `InteractableHighlight _highlight`, `ServicePointHud _hud` (может быть null).
- `PointId`, `ServiceTypeId`, `CarSpot` — геттеры для EntryPoint и `CarAgents`.
- `Construct(IServicePointService service)` — запоминает сервис. `IInteractable`: `BeginInteraction` → `service.TryOccupy(_pointId, Player)`; `EndInteraction` → `Vacate(_pointId, Player)`; `IsInteractable` → точка существует и не занята **работником**. `ApproachPosition/Rotation` → `_approachPoint`.
- `OnDrawGizmos`: car spot (бокс 2×4 + стрелка), approach point.

**`ServicePointHud : MonoBehaviour`** — world-space индикаторы над точкой (дочерний world-space Canvas):
- `[SerializeField] Image _progressFill` (Filled), `GameObject _progressRoot`, `GameObject _awaitingIcon` («!»), `GameObject _occupiedIcon` (опц.).
- `public void Render(ServicePointState state, float progress, bool occupied)` — включает/выключает элементы, `fillAmount`. Вызывать часто — без аллокаций.
- Канвас поворачивать к камере **не** нужно (изометрия, фиксированный угол) — просто наклонить в сцене.

**`ServicePointPresenter : ITickable, IDisposable`** (C#; ctor: `ServicePointView`, `ServicePoint`) — каждый тик `view.Hud.Render(...)` (дёшево), подсветка «занято игроком» не нужна.

**`BarrierArm : MonoBehaviour`** — визуал шлагбаума: `[SerializeField] Transform _arm`, `float _openAngle = 80`, `float _speed = 240` (°/с, unscaled). `SetOpen(bool)`; анимация — без `Update`: метод `Animate(float unscaledDt)` вызывает `ServicePointPresenter`, если у view есть `BarrierArm` (open, когда state `Servicing` или `Clearing`).

### 6.2 `Presentation/Traffic/`

**`LocationLayout : MonoBehaviour`** — разметка локации на сцене:
`string _locationId`, `ServicePointView _barrier`, `ServicePointView[] _servicePoints`, `Transform _spawnPoint`, `Transform _exitPoint`, `Transform[] _queueSlots` (0 = голова у развилки), `Transform[] _parkingSlots`.
`bool TryResolve(in CarDestination d, out Transform target)`; `OnDrawGizmos` — номера слотов, линии очереди.

**`CarVisualCatalog : ScriptableObject`** (`"AutoService/Car Visual Catalog"`, лежит в Presentation): массив `{ string carTypeId; CarView prefab; }`; `bool TryGetPrefab(string id, out CarView prefab)`.

**`CarView : MonoBehaviour`** — машина: `[SerializeField] NavMeshAgent _agent`, `float _alignSpeed = 360` (°/с), `float _arrivalTolerance = 0.3`. Методы для `CarAgents`: `Place(Vector3, Quaternion)` (через `_agent.Warp`), `Drive(Transform target)`, `bool TickArrival(float dt)` (true один раз при прибытии и выравнивании по `target.rotation`, yaw-only), `ResetForPool()`.

**`CarAgents : ICarAgents, ITickable, IDisposable`** (C#; ctor: `LocationLayout`, `CarVisualCatalog`, `Transform poolRoot`):
- Пул на каждый `carTypeId` (`ObjectPool<CarView>`, `defaultCapacity` 4); `Dictionary<int, CarView>` активных + список для тика.
- `Spawn` → взять из пула, `Place` в `_spawnPoint`. `MoveTo` → `layout.TryResolve` → `Drive`. `Tick` → для каждой машины `TickArrival` → `Arrived?.Invoke(carId)`. Вызов события **после** прохода по списку или безопасно к изменениям (обработчик может вызвать `MoveTo`/`Despawn`) — без аллокаций (например, буфер прибывших id).
- Неизвестный тип/нет префаба/нет цели → ошибка в лог, машина не теряется (сразу `Arrived`, чтобы поток не завис) — задокументируй.

### 6.3 `Presentation/Hud/`

**`BalanceView : MonoBehaviour`** — `[SerializeField] TMP_Text _label`; `SetText(string)`.
**`BalancePresenter : IDisposable`** — подписан на `IWalletService.BalanceChanged`, пишет `MoneyFormatter.Format` (только при изменении). Временный HUD до модуля 09.

## 7. Интеграция в `GameplayEntryPoint`

Новые поля `[Header("Location")]`: `LocationLayout _location1`, `CarVisualCatalog _carVisuals`, `Transform _carPoolRoot`; `[Header("HUD")]`: `BalanceView _balanceView`.

Шаги (после `RegisterPlayer`):
1. **`RegisterPoints()`**: `new ServicePointService(wallet, bus)` → для барьера и каждой точки `_location1` взять `ServiceTypeSettings` по `ServiceTypeId` (нет → ошибка с именем view), собрать `ServicePointDefinition` (`LocationId` из layout), `Register`; `view.Construct(service)`; `new ServicePointPresenter(view, point)` → `Register`. Проверка: у барьера `Kind == Barrier`, у точек — `Service`.
2. **`RegisterTraffic()`**: `new CarAgents(...)`, `new LocationTraffic(definition из layout, ...)` → `Register` оба (порядок тика: `ServicePointService` → `LocationTraffic` → `CarAgents` → presenters).
3. **`RegisterHud()`**: `BalancePresenter`.
Ошибки ссылок — как в 02: понятный лог, модуль пропускается.

## 8. Тесты (EditMode)

- `ServicePointTests`: без исполнителя нет приёма; `AcceptDelay` сбрасывается при уходе; `OrderAccepted` с ценой; прогресс только при исполнителе; `Clearing` → `Idle` через `ClearDelay`; `TryOccupy` занятой другим → false; `NotifyCarArrived` чужой машины → исключение.
- `EntryQueueTests`, `ParkingLotTests`: ёмкость, сдвиг, `Shifted`, резерв/освобождение.
- `CarTests`: допустимые/недопустимые переходы, терпение не ниже 0, `PatienceDepleted` один раз.
- `LocationTrafficTests` (фейки `ICarAgents`, `IRandom`, реальные `ServicePointService`/`EventBus`/`WalletService`): голова едет прямо на свободную точку; точка занята → к шлагбауму; парковка полна → ждёт; припаркованная машина в приоритете перед головой; полный цикл «спавн → шлагбаум → парковка → точка → выезд» начисляет 2 оплаты и публикует `ServiceCompletedEvent`; без исполнителя деньги не начисляются.

## 9. Editor setup (пользователь) — включи в PR

> **Иерархия и «на какой объект какой компонент» — строго как в дереве.** Координаты — ориентир для whitebox, можно двигать.

**9.1 Ассеты (`Assets/_Project/Configs/`)**
- `ServiceTypes/ST_Parking` (Kind Barrier, price 2, duration 1, accept 0.4, clear 1.5), `ST_Wash` (Service, price 12, duration 6). (`ST_Oil`/`ST_Tires` — позже, когда будут точки.)
- `CarTypes/CT_Sedan` (weight 60, ×1, patience 60), `CT_Suv` (30, ×1.5, 60), `CT_Sport` (10, ×2.5, 40).
- `GameConfig`: заполнить массивы Service Types / Car Types; Traffic по умолчанию.
- `CarVisualCatalog` → `Assets/_Project/Configs/CarVisualCatalog` с тремя записями (после 9.3).

**9.2 Слои и NavMesh**
- Слой 8 = `Road`. В *Window → AI → Navigation → Agents* добавить тип **Car**: Radius 1.1, Height 1.5, Step 0.3, Max Slope 20.
- На `[Navigation]` уже есть NavMeshSurface (Humanoid) — оставить. Добавить **второй** компонент NavMeshSurface на тот же объект: Agent Type **Car**, Collect Objects All, **Include Layers = Road**. Bake оба после разметки.
- `GameplayEntryPoint._groundMask` = Ground **+ Road** (игрок кликает и по дороге).

**9.3 Префабы машин (`Assets/_Project/Prefabs/Cars/`)**
```
Car_Sedan (Empty)            ← NavMeshAgent (Agent Type Car, Speed 7, Angular 200, Accel 10, Stopping 0.2,
│                               Obstacle Avoidance: None) + CarView (Agent)
└── Body (Cube 2×1.2×4, y=0.6, без коллайдера, синий материал)
```
Дублировать → `Car_Suv` (Body 2.2×1.6×4.4, зелёный), `Car_Sport` (Body 2×0.9×4.2, красный). Внести в `CarVisualCatalog`.

**9.4 Сцена `Gameplay` (вид сверху, дорога вдоль X)**
```
Location_1 (Empty)                          ← LocationLayout (locationId "loc1"; ссылки ниже)
├── Road        (Cube 64×0.1×5 в (0,0,-20), слой Road)           ← верх на y=0.05, чуть выше пола (без z-fighting)
├── Driveway    (Cube 5×0.1×18 в (3,0,-9), слой Road)            ← съезд к мойке, z от -18 до 0
├── ParkingPad  (Cube 18×0.1×7.5 в (16,0,-26.25), слой Road)     ← примыкает к дороге (z от -30 до -22.5)
├── SpawnPoint  (Empty (-30,0,-20), поворот Y=90)
├── ExitPoint   (Empty (30,0,-20), Y=90)
├── QueueSlots
│   ├── Q0 (-3,0,-20) Y=90   ← голова у развилки
│   ├── Q1 (-9,0,-20) Y=90
│   ├── Q2 (-15,0,-20) Y=90
│   └── Q3 (-21,0,-20) Y=90
├── ParkingSlots
│   ├── P0 (10,0,-27) Y=180   … P3 (22,0,-27) шаг 4 по X
├── Barrier (Cube 0.5×1×0.5 в (8,0.5,-17.5), слой Interactable, BoxCollider)
│                              ← InteractableHighlight (Renderers = этот куб) + ServicePointView
│                                 (pointId "loc1_barrier", serviceTypeId = id из ST_Parking,
│                                  Car Spot = CarSpot, Approach Point = WorkSpot, Highlight, Hud) + BarrierArm (Arm)
│   ├── Arm      (Cube 4×0.15×0.15, pivot у столба — сделай Empty-родителя "ArmPivot" и назначь его в BarrierArm)
│   ├── CarSpot  (Empty (8,0,-20) Y=90)
│   ├── WorkSpot (Empty (8,0,-16.5), синяя ось к столбу)
│   └── Hud      (Canvas World Space 2×1, над столбом) ← ServicePointHud
│       ├── ProgressRoot → ProgressFill (Image Filled Horizontal)
│       └── AwaitingIcon (TMP "!" крупно, жёлтый)
└── Wash (Empty в (3,0,-3))   ← InteractableHighlight (Renderers = PillarL, PillarR, Roof)
                                 + ServicePointView (pointId "loc1_wash_1", serviceTypeId = id из ST_Wash)
    │                            Компоненты — на пустом родителе Wash; коллайдеры — на детях (луч найдёт родителя).
    ├── PillarL  (Cube 0.5×3×5 в локальных (-2.5,1.5,0), слой Interactable, BoxCollider)
    ├── PillarR  (Cube 0.5×3×5 в (2.5,1.5,0), слой Interactable, BoxCollider)
    ├── Roof     (Cube 5.5×0.3×5 в (0,3.15,0), слой Interactable, BoxCollider)   ← «арка», машина въезжает под неё
    ├── CarSpot  (Empty (0,0,0), Y=0 — машина смотрит на север, вглубь арки)
    ├── WorkSpot (Empty (3.5,0,0), синяя ось к центру арки)
    └── Hud      (как у барьера, над крышей) ← ServicePointHud
```
- `[EntryPoint]`: Location 1 = `Location_1`, Car Visuals = `CarVisualCatalog`, Car Pool Root = новый пустой `[CarPool]`, Balance View = см. ниже.
- **HUD**: `Canvas` (Screen Space Overlay, Canvas Scaler 1920×1080 match 0.5) → `BalanceText` (TMP, правый верх) ← `BalanceView`.
- Перепечь **оба** NavMeshSurface.

**9.5 Проверка (Play)**
1. Каждые ~7 с появляется машина и встаёт в очередь; очередь заполняется до 4.
2. Голова едет **прямо на мойку** (она свободна) → над мойкой «!» → кликни по мойке → персонаж встаёт на WorkSpot → через 0.4 с баланс **+$12×множитель** → полоса прогресса → машина уезжает к выезду и исчезает.
3. Пока мойка занята, следующая голова едет к **шлагбауму** → «!» над шлагбаумом → беги к шлагбауму → **+$2** → шлагбаум поднимается → машина паркуется. Когда мойка освобождается, машина с парковки едет на мойку (раньше головы очереди).
4. Уйди с мойки посреди мойки → прогресс замирает; вернись → продолжается.
5. Console без ошибок.

**9.6 Коммит в ветку**: `.meta`, сцена, префабы, конфиги, материалы, `TagManager.asset`, `NavMeshSettings`/`ProjectSettings` изменения, NavMesh-ассеты.

## 10. Критерии приёмки

- [ ] Компиляция без ошибок/warning'ов, EditMode-тесты зелёные.
- [ ] В Domain/Services нет Unity; Presentation общается с трафиком только через `ICarAgents` и id.
- [ ] Нет аллокаций в тиках (пулы, предвыделенные коллекции, без LINQ/замыканий/конкатенаций).
- [ ] Сценарий 9.5 проходит.
- [ ] XML-доки, `// Why:` на неочевидных решениях (особенно в `LocationTraffic`).

## 11. Git

- Ветка `feature/03-service-loop`. Коммиты по слоям: `Add service point domain`, `Add traffic domain: car, entry queue, parking`, `Add point and traffic config`, `Add service point service and bus events`, `Add location traffic orchestrator`, `Add service point and traffic tests`, `Add point views and HUD`, `Add car views, catalog and pooled agents`, `Add balance HUD`, `Wire service loop into GameplayEntryPoint`, `Default highlight to base color tint`.
- PR `03: Service loop` + Editor setup (§9) + отклонения с причинами.
