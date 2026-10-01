# Промпт 03b — Трафик v2: маршруты, зоны слияния, оплата парковки за время

**Ветка:** `feature/03b-traffic-routing` (от актуального `main`) · **PR:** в `main`
**Перед началом:** прочитай `CLAUDE.md`, `Docs/GDD.md` §3 (новая схема локации!), `Docs/TDD.md` §4.3–4.5, `Docs/ARCHITECTURE.md` §6.

---

## 1. Контекст и цель

Модуль 03 работает, но машины едут по **кратчайшему пути NavMesh**: проезжают сквозь шлагбаум, очередь и друг друга. Переходим на **одностороннюю схему с маршрутами** (GDD §3):

```
 север
   ┌───────────── выездная полоса (z=4) ───────────────────┐
   │         ↑                                             │
   │      [Мойка] (проездной бокс)                         │
   │         ↑                                             ↓
   ├───── полоса к боксам (z=-6, на восток) ──(S)──…──────(J)  ← слияние с выездной
   ↑ (lane A)                                ↑             ↓ (lane D)
   │             [P0][P1][P2][P3]  ← аллея (z=-14, на запад) ← [Ворота, авто] (G)
   │(F)     [Шлагбаум выезда] ↑                                ↑
 ══╪═ очередь Q3..Q0 (z=-19, на восток) ═══════════════════════┘
 ═══════════════ сквозная полоса (z=-24) ══════════════════════(D)═══► Выезд
```

Изменения геймплея:
1. **Въезд на парковку — автоматические ворота** (зона `G`, без исполнителя, бесплатно; стрела открывается сама, пока машина в зоне).
2. **Шлагбаум выезда с парковки** — точка `Barrier` с исполнителем. Через него проходят **все** покидающие парковку. Оплата **за время стоянки**: `(BasePrice + PricePerSecond × секунд на месте) × множитель машины`.
3. Голова очереди: «услуга» + бокс свободен + нет готовых припаркованных к нему → **сразу в бокс**; иначе есть место → **на парковку** (через ворота); иначе ждёт.
4. Готовая к выезду машина (таймер стоянки истёк) с парковки уходит, только когда **свободен шлагбаум выезда** (и, для «услуги», **свободен бокс** — резервируются оба сразу). После оплаты: «услуга» → в бокс, «только парковка» → по полосе к боксам мимо них → (J) → выезд.
5. Движение — по **графу дорожных узлов** (`RoadNode`), NavMesh только между соседними узлами. **Зоны слияния** (`S`, `J`, `G`) — одна машина за раз, остальные ждут на узле перед зоной.

Services продолжает говорить только «куда» (`ICarAgents.MoveTo`); «как» — Presentation.

## 2. Domain / Services

### 2.1 Машина (`Domain/Traffic`)
- `CarState`: `Arriving, InQueue, ToParking, Parked, ToParkingExit, AtParkingExit, ToPoint, AtPoint, Leaving` (`ToBarrier/AtBarrier` удалить).
- `Car`:
  - `SendToParking(int slot, float stay)` — из `InQueue` (сразу на место, ворота логики не имеют).
  - `MarkArrived` для `ToParking` → `Parked` (+`ParkedAtTime`), для `ToParkingExit` → `AtParkingExit`, `ToPoint` → `AtPoint`.
  - `SendToParkingExit(string nextPointId)` — из `Parked` при `IsReadyToLeaveParking`; `nextPointId == null` для «только парковка»; освобождает `ParkingSlot`, запоминает `NextPointId`.
  - `ContinueFromParkingExit()` — из `AtParkingExit`: есть `NextPointId` → `ToPoint` (`TargetPointId = NextPointId`), иначе → `Leaving`.
  - `LeaveParking()` — удалить (заменено шлагбаумом выезда).
  - Терпение тикает в `InQueue`, `Parked` (после стоянки), `AtParkingExit` и `AtPoint` (последние два — пока точка `AwaitingAccept`, это решает `LocationTraffic`).

### 2.2 Цена за время
- `Domain/Economy/PriceFormula.TimeBased(Money basePrice, double pricePerSecond, double seconds, double multiplier) → Money` — `(base + perSecond × seconds) × multiplier`, округление как у `Money * double`, отрицательные/NaN аргументы → `ArgumentOutOfRangeException`. Тесты.
- `ServicePointDefinition` и `ServiceTypeSettings` (+`ServiceTypeConfig._pricePerSecond`, `[Min(0)]`, default 0): поле `double PricePerSecond`.

### 2.3 `LocationTraffic`
- `LocationTrafficDefinition.BarrierPointId` → **`ParkingExitPointId`** (Kind `Barrier`).
- `CarDestinationKind`: `QueueSlot, ParkingSlot, ParkingExit, Point, Exit` (`Barrier` → `ParkingExit`).
- Голова очереди (п.1.3): парковка — `TryReserve` слота → `SendToParking(slot, NextParkingStay())` → `MoveTo(ParkingSlot(slot))`.
- Готовые припаркованные, FIFO по готовности (как сейчас `_readyParkedCars`):
  - «только парковка»: шлагбаум `Idle` → `TryReserve(car, fee)` → `parking.Release` → `SendToParkingExit(null)` → `MoveTo(ParkingExit)`;
  - «услуга»: шлагбаум `Idle` **и** свободен бокс → резерв обоих (бокс с ценой услуги, шлагбаум с fee) → `SendToParkingExit(pointId)` → `MoveTo(ParkingExit)`. Если резерв бокса не удался после резерва шлагбаума — откат (порядок: сначала проверить оба `IsAvailable`, потом резервировать).
  - `fee = PriceFormula.TimeBased(exit.BasePrice, exit.PricePerSecond, _time - car.ParkedAtTime, car.Type.PriceMultiplier)`.
- Прибытие `AtParkingExit` → `exit.NotifyCarArrived`. Завершение шлагбаума → `ContinueFromParkingExit()` → `MoveTo(Point(id))` или `MoveTo(Exit)`. Публикация `ServiceCompletedEvent` — как сейчас.
- Комментарии `// Why:` на правилах приоритета и двойном резерве.

### 2.4 Граф маршрутов — чистый C#, `Domain/Traffic/Routing/RouteGraph.cs`
```csharp
public sealed class RouteGraph
{
    public RouteGraph(int nodeCount);
    public void AddEdge(int from, int to);          // направленное ребро
    public void Build();                             // BFS из каждого узла → таблица next-hop (n×n int), один раз
    public bool TryGetPath(int from, int to, List<int> pathBuffer); // заполняет буфер узлами ПОСЛЕ from до to включительно; без аллокаций
    public bool HasPath(int from, int to);
}
```
Тесты: линейная цепочка, развилка, недостижимый узел, `from == to` (пустой путь, true), одностороннее ребро не проходится в обратную сторону.

## 3. Presentation — `Presentation/Traffic/Routing/`

**`RoadNode : MonoBehaviour`** — узел дороги.
- `[SerializeField] RoadNode[] _next` (направленные связи), `[SerializeField] TrafficZone _zone` (null = вне зоны), `[SerializeField, Min(0.1f)] float _passRadius = 1.5f` (на промежуточном узле машина не тормозит, а переключается на следующий, когда ближе этого радиуса).
- `Index` (выставляет `LocationLayout` при сборке графа), `Next`, `Zone`, `PassRadius`.
- `OnDrawGizmos`: сфера (цвет зоны или белая), **стрелки к `_next`**, подпись имени (`UnityEditor.Handles.Label` под `#if UNITY_EDITOR`).

**`TrafficZone : MonoBehaviour`** — зона «одна машина за раз».
- `[SerializeField] Color _gizmoColor`, `[SerializeField] BarrierArm _gate` (опц., для ворот въезда).
- `bool TryEnter(int carId)` (true, если свободна или уже наша), `void Exit(int carId)`, `bool IsOccupied`, `int OccupantCarId`.
- `TickVisual(float unscaledDt)` — если есть `_gate`: `SetOpen(IsOccupied)` + `Animate`.

**`LocationLayout`** (переделать):
- Поля: `string _locationId`, `ServicePointView _parkingExit`, `ServicePointView[] _servicePoints`, `RoadNode _spawnNode`, `RoadNode _exitNode`, `RoadNode[] _queueSlots`, `RoadNode[] _parkingSlots`.
- `BuildGraph()` (вызывает `CarAgents` один раз): `GetComponentsInChildren<RoadNode>(true)` → индексы → `RouteGraph` с рёбрами из `_next` → `Build()`. Проверки (в `Validate`): у `CarSpot` каждой точки есть `RoadNode`; все слоты/спавн/выезд — узлы этого графа; от спавна достижимы все слоты; от каждого бокса и шлагбаума достижим выезд; описание проблемы — с именами узлов.
- `bool TryResolveNode(in CarDestination d, out RoadNode node)`.
- Gizmos: как сейчас + граф рисуют сами узлы.

**`CarView`** — следование по пути:
- `FollowPath(IReadOnlyList<RoadNode> path)` — копирует узлы во внутренний **предвыделенный** буфер (без аллокаций), едет к `path[0]`.
- `TickArrival(dt, ZoneGate gate)`: промежуточный узел ближе `PassRadius` → следующий; финальный — торможение + выравнивание по `forward` узла (как сейчас). **Перед переключением на узел** с зоной, отличной от текущей: `zone.TryEnter(carId)`; не удалось → `isStopped = true`, ждём (повторяем попытку каждый тик). Покинув узлы зоны → `zone.Exit(carId)`.
- `CurrentNode` — последний достигнутый узел (стартовый — спавн).
- Avoidance остаётся `None` (порядок обеспечивают зоны и резервы).

**`CarAgents`**:
- `MoveTo` → `layout.TryResolveNode` → `graph.TryGetPath(car.CurrentNode.Index, target.Index, buffer)` → `view.FollowPath`. Нет пути → ошибка в лог + снап к цели + `Arrived` (как сейчас для «нет цели»).
- В `Tick` — `TickVisual` всех `TrafficZone` локации (unscaled).
- `Despawn` → освободить зону, если машина в ней.

**`ServicePointPresenter`**: `BarrierArm` у шлагбаума выезда работает как сейчас.

## 4. Editor-инструмент — `Bootstrap/Editor/WhiteboxLocationBuilder.cs`

Меню **`AutoService → Whitebox → Build Location 1 Roads`** (`[MenuItem]`, с `Undo`). Создаёт под выбранным `LocationLayout` (или ищет объект `Location_1`) дочерний `Roads_v2`:
- **Дорожные поверхности** (Cube, слой `Road`, материал не трогать):
  | Имя | Position | Scale |
  |---|---|---|
  | `Surface_Road` | (0,0,-21.5) | (72,0.1,8) |
  | `Surface_LaneA` | (-3,0,-11) | (5,0.1,13) |
  | `Surface_ServiceLane` | (11.5,0,-6) | (34,0.1,5) |
  | `Surface_Bays` | (16,0,1.5) | (24,0.1,10) |
  | `Surface_LaneD` | (26,0,-12) | (5,0.1,13) |
  | `Surface_Parking` | (11,0,-13) | (22,0.1,9) |
- **Зоны**: пустые объекты с `TrafficZone`: `Zone_S`, `Zone_J`, `Zone_G` (у `Zone_G` — создать стрелу ворот: столб Cube (0.5×1×0.5) в (22.5,0.5,-16), `ArmPivot` на верхушке, `Arm` Cube (4×0.15×0.15) с локальной позицией (-2,0,0), **без коллайдеров**, `BarrierArm` (Open Axis Z) на столбе; ссылка `_gate`).
- **Узлы** (`RoadNode`, поворот = направление движения, связи `→`):

| Узел | Position | Rot Y | Next | Зона |
|---|---|---|---|---|
| `N_Spawn` | (-34,0,-19) | 90 | Q3 | |
| `Q3` | (-24,0,-19) | 90 | Q2 | |
| `Q2` | (-18,0,-19) | 90 | Q1 | |
| `Q1` | (-12,0,-19) | 90 | Q0 | |
| `Q0` | (-6,0,-19) | 90 | F1, R1 | |
| `F1` | (-3,0,-15) | 0 | A1 | |
| `A1` | (-3,0,-9) | 0 | S0 | |
| `S0` | (-1,0,-6) | 90 | S1 | |
| `S1` | (2,0,-6) | 90 | L6 | **S** |
| `L6` | (6,0,-6) | 90 | `<CarSpot мойки>`, L12 | |
| `L12` | (12,0,-6) | 90 | L18 | |
| `L18` | (18,0,-6) | 90 | L24 | |
| `L24` | (24,0,-6) | 90 | J1 | |
| `E6` | (6,0,4) | 90 | E26 | |
| `E26` | (26,0,4) | 180 | J1 | |
| `J1` | (26,0,-6) | 180 | D1 | **J** |
| `D1` | (26,0,-14) | 180 | D2 | |
| `D2` | (26,0,-24) | 90 | N_Exit | |
| `N_Exit` | (34,0,-24) | 90 | — | |
| `R1` | (4,0,-19) | 90 | R2 | |
| `R2` | (18,0,-19) | 90 | G1 | |
| `G1` | (20,0,-16) | 0 | K18 | **G** |
| `K18` | (18,0,-14) | 270 | P3, K14 | |
| `K14` | (14,0,-14) | 270 | P2, K10 | |
| `K10` | (10,0,-14) | 270 | P1, K6 | |
| `K6` | (6,0,-14) | 270 | P0, K2 | |
| `K2` | (2,0,-14) | 0 | `<CarSpot шлагбаума выезда>` | |
| `P3` | (18,0,-11) | 0 | K14 | |
| `P2` | (14,0,-11) | 0 | K10 | |
| `P1` | (10,0,-11) | 0 | K6 | |
| `P0` | (6,0,-11) | 0 | K2 | |

- **Точки** (найти `ServicePointView` по `pointId` в сцене, `Undo.RecordObject`):
  - мойка `loc1_wash_1`: переместить корень так, чтобы `CarSpot` оказался в (6,0,-1), поворот 0; на `CarSpot` добавить `RoadNode` (Next = `E6`), связать `L6 → CarSpot`. `WorkSpot` оставить локально как есть.
  - шлагбаум `loc1_barrier`: переименовать pointId в **`loc1_parking_exit`**, переместить столб в (0,0.5,-10), `CarSpot` → (2,0,-10) поворот 0, `RoadNode` (Next = `S1`), связать `K2 → CarSpot`; `WorkSpot` → (-1,0,-10), лицом на восток; стрелу развернуть вдоль +X (`Arm` локально (2,0,0) относительно `ArmPivot`), `BarrierArm` Open Axis = Z.
- Заполнить `LocationLayout`: spawn/exit/queue/parking/parkingExit/servicePoints.
- Повторный запуск: удаляет старый `Roads_v2` (Undo) и строит заново; идемпотентно по точкам.
- Координаты — в одном статическом массиве-таблице в начале файла (легко править), комментарий «whitebox layout v2, see GDD §3».

> Why Editor-скрипт: ~40 узлов со связями руками — 1 час и ошибки; скрипт — 1 клик и воспроизводимо. После замены на ассеты узлы двигаются руками.

## 5. Интеграция и уборка
- `GameplayEntryPoint`: `LocationTrafficDefinition` из `ParkingExit.PointId`; проверка Kind = Barrier.
- Старые поверхности `Road`/`Driveway`/`ParkingPad` и `Transform`-слоты больше не используются — инструмент **не удаляет** их сам, это делает пользователь (Editor setup).

## 6. Тесты (обновить/добавить)
- `RouteGraphTests`, `PriceFormula.TimeBased`.
- `CarTests`: новые переходы (`SendToParking` из очереди, `SendToParkingExit` только когда готов, `ContinueFromParkingExit` в обе стороны).
- `LocationTrafficTests`: голова на свободный бокс; иначе на парковку (без шлагбаума); «только парковка»: стоянка → шлагбаум выезда → **оплата зависит от времени** → выезд; «услуга» после стоянки резервирует шлагбаум **и** бокс одновременно, ждёт, если один из них занят; без исполнителя на шлагбауме машины не покидают парковку; приоритет готовых припаркованных.

## 7. Editor setup (пользователь) — включи в PR
1. `ST_Parking`: Base Price `2`, **Price Per Second `0.5`**.
2. **Удалить** старые `Road`, `Driveway`, `ParkingPad`, `QueueSlots`, `ParkingSlots` из `Location_1`.
3. Меню **AutoService → Whitebox → Build Location 1 Roads**. Проверить в Scene View стрелки графа.
4. `Plane` (пол): Scale (8,1,8). `CameraRig` → Bounds Min (-36,-28), Max (36,10).
5. `[Navigation]` → **Bake** обоих NavMeshSurface.
6. Ctrl+S → Play:
   - машины в очереди на z=-19, голова сворачивает к мойке по полосе A → S → L6 → мойка → выезд вверх → по выездной полосе → J → вниз → выезд; **шлагбаум не пересекает**;
   - на парковку — через ворота G (стрела сама поднимается), паркуется носом на север;
   - после стоянки — к шлагбауму выезда, ждёт исполнителя → **+$ зависит от времени стоянки** → «услуга» на мойку через S, «только парковка» мимо мойки по полосе к J → выезд;
   - в зонах S/J одна машина, вторая ждёт перед зоной.
7. Закоммитить в ветку сцену, конфиги, `.meta`.

## 8. Критерии приёмки
- [ ] Тесты зелёные (включая новые), компиляция без warning'ов.
- [ ] Domain/Services без Unity; `RouteGraph` — чистый C#.
- [ ] Ноль аллокаций в тиках (пути — в предвыделенные буферы).
- [ ] Сценарий §7.6 проходит; машины не пересекают шлагбаум/очередь, не едут встречными по боксу.

## 9. Git
Ветка `feature/03b-traffic-routing`; коммиты: `Add route graph`, `Add time-based parking price`, `Rework car flow: auto entry gate, paid parking exit`, `Update traffic tests`, `Add road nodes and traffic zones`, `Follow road paths in car views and agents`, `Rework location layout for road graph`, `Add whitebox location builder`, `Wire parking exit into GameplayEntryPoint`. PR `03b: Traffic routing` + Editor setup.
