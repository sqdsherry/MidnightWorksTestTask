# Промпт 03b.2 — доработка: компактная схема трафика v3

**Ветка:** та же `feature/03b-traffic-routing` (поверх твоих коммитов 03b) · **PR:** тот же
**Перед началом:** перечитай `Docs/GDD.md` §3 — схема и правила **v3** (заменяют v2).

---

## 1. Что меняется и почему

Пользователь протестировал v2: схема растянута, много пустых полос, шлагбаум можно объехать. Возвращаемся к компактной схеме на базе v1:

```
                    ┌──────── верхняя дорога (z=8) ─────────────┐
          [выезд мойки, вперёд] ──► [Шлагбаум 2] ──┐            │
                    ↑                              ↓            │ дорога выезда (x=30)
                 [Мойка]  (проездная)        ┌─ ПАРКОВКА ─┐     │
                    ↑ буфер WB0, WB1         │ P0 P1 P2 P3│ ─(J)┤ автовыезд
                    ↑ съезд (x=3)       (M)──┘            │     │
 Спавн ═ Q3 Q2 Q1 Q0 ═(F)═════► [Шлагбаум 1] ─┘              │
 ═══════════════════════════════════════════════════════════════┴══► Выезд (z=-24)
```

**Сохраняется из 03b без изменений:** `RouteGraph`, `RoadNode`, `TrafficZone`, следование по пути в `CarView`/`CarAgents`, `PriceFormula.TimeBased`, `CancelReservation`, Editor-builder (меняется только таблица и шаги точек).

**Удаляется:** шлагбаум выезда, `ParkingExit`-логика, маршрут «парковка → бокс», зоны S/G, ворота-автомат (зона без `_gate`; поле `_gate` в `TrafficZone` оставить — пригодится).

## 2. Правила v3 (Domain/Services)

### 2.1 Планы визита
`CarVisitPlan` (enum в `Domain/Traffic`): `ParkOnly`, `WashOnly`, `WashThenPark`. Выбор при спавне по весам из `TrafficSettings`: `ParkOnlyWeight = 35`, `ServiceOnlyWeight = 35`, `ServiceThenParkWeight = 30` (вместо `ParkOnlyChance`). Если в локации нет точек услуг → всегда `ParkOnly`. «Мойка» = любая точка-услуга локации (как сейчас, случайный тип из имеющихся).

### 2.2 Два въездных шлагбаума
- `LocationTrafficDefinition`: `MainEntranceId` (шлагбаум 1, с дороги), `ServiceEntranceId` (шлагбаум 2, после бокса); оба — `PointKind.Barrier`, валидация как сейчас.
- Оплата **при приёме на шлагбауме** (обычный `OrderAccepted`): цена резерва = `PriceFormula.TimeBased(base, perSecond, plannedStay, multiplier)`, где `plannedStay = NextParkingStay()` выпадает в момент резерва и сохраняется в машине.
- После завершения шлагбаума машина едет на зарезервированное место (`ParkingSlot(slot)`); на месте — стоянка `plannedStay` (терпение не тратится); затем **автовыезд**: освободить слот → `MoveTo(Exit)` → `Leaving`.

### 2.3 Буфер перед точкой (`Domain/Traffic/EntryQueue` переиспользовать)
- У каждой точки-услуги — буфер из `N` мест (`LocationTrafficDefinition.ServiceBufferCapacity`, по сцене = 2; общий для локации на одну точку — пока точка одна, сделай `Dictionary<pointId, EntryQueue>` либо один буфер на точку из layout — выбери и задокументируй).
- Голова очереди с планом `WashOnly`/`WashThenPark`:
  1. точка свободна **и** её буфер пуст → `TryReserve(point)` → `MoveTo(Point)`;
  2. иначе в буфере есть место → `EnqueueBuffer` → `MoveTo(BufferSlot(pointId, i))`;
  3. иначе ждёт в голове.
- Голова буфера (доехала): точка `Idle` → reserve → `MoveTo(Point)`; буфер сдвигается (`Shifted` → `MoveTo(BufferSlot(...))`).
- Голова очереди `ParkOnly`: шлагбаум 1 `Idle` **и** есть место → резерв обоих → `MoveTo(MainEntrance)`; иначе ждёт.

### 2.4 После точки-услуги
- `WashOnly` → `Leave()` → `MoveTo(Exit)`.
- `WashThenPark` → если шлагбаум 2 `Idle` и есть место → резерв обоих → `MoveTo(ServiceEntrance)`; **иначе** → `Leave()` → `MoveTo(Exit)` + событие `ParkingRefusedEvent { CarId; LocationId }` (для будущего облачка «Нет мест»). `// Why:` машина не может ждать на выезде из бокса — перегородит дорогу.

### 2.5 Машина (`Car`)
Состояния: `Arriving, InQueue, ToBuffer, InBuffer, ToPoint, AtPoint, ToEntrance, AtEntrance, ToParking, Parked, Leaving`. Поля: `Plan`, `TargetPointId` (точка или шлагбаум), `ParkingSlot`, `PlannedStay`, `ParkingStayLeft`. Переходы проверяют исходное состояние (как сейчас). Терпение тикает: `InQueue`, `InBuffer`, `AtEntrance`/`AtPoint` (пока точка `AwaitingAccept`). Во время стоянки — нет.

### 2.6 `CarDestinationKind`
`QueueSlot, BufferSlot (Index + PointId), Entrance (PointId), ParkingSlot, Point, Exit`.

### 2.7 Тесты (переписать трафик)
Голова «парковка» → шлагбаум 1 → оплата = f(запланированное время) → место → стоянка → автовыезд. Голова «мойка» при занятой мойке уходит в буфер, очередь продолжает двигаться (следующая «парковка» проходит к шлагбауму). Буфер сдвигается, голова буфера получает мойку. «Мойка → парковка» после мойки → шлагбаум 2 → место; при полной парковке — уезжает + `ParkingRefusedEvent`. Без исполнителя на шлагбауме оплаты нет и машина не заезжает. Нет точек услуг → только «парковка».

## 3. Presentation

- `LocationLayout`: поля `ServicePointView _mainEntrance`, `ServicePointView _serviceEntrance`, `ServicePointView[] _servicePoints`, `RoadNode _spawnNode`, `RoadNode _exitNode`, `RoadNode[] _queueSlots`, `RoadNode[] _parkingSlots`. У `ServicePointView` — новое поле `RoadNode[] _bufferSlots` (0 = ближайшее к точке). Валидация: от спавна достижимы буферы, шлагбаумы, места; от точек/мест достижим выезд; от точки достижим шлагбаум 2.
- `TryResolveNode` под новые `CarDestinationKind`.
- `GameplayEntryPoint`: регистрировать оба шлагбаума (`Kind == Barrier`), определение трафика с двумя id и ёмкостью буфера из view мойки.

## 4. Editor-builder — новая таблица (заменить)

Меню то же: **AutoService → Whitebox → Build Location 1 Roads**. Удаляет старый `Roads_v2`, строит `Roads_v3`.

**Поверхности** (слой Road):
| Имя | Position | Scale |
|---|---|---|
| `Surface_Road` | (0,0,-21.5) | (72,0.1,8) |
| `Surface_Driveway` | (3,0,-4) | (5,0.1,28) |
| `Surface_TopRoad` | (17,0,8) | (32,0.1,5) |
| `Surface_Entrance2Lane` | (11,0,3) | (4,0.1,6) |
| `Surface_Parking` | (19,0,-8.25) | (20,0.1,18.5) |
| `Surface_ExitRoad` | (30,0,-8) | (5,0.1,34) |

**Зоны:** `Zone_M` (слияние въездов), `Zone_J` (автовыезд → дорога выезда). Без ворот.

**Узлы:**
| Узел | Position | Rot Y | Next | Зона |
|---|---|---|---|---|
| `N_Spawn` | (-32,0,-19) | 90 | Q3 | |
| `Q3` | (-21,0,-19) | 90 | Q2 | |
| `Q2` | (-15,0,-19) | 90 | Q1 | |
| `Q1` | (-9,0,-19) | 90 | Q0 | |
| `Q0` | (-3,0,-19) | 90 | F1, R1 | |
| `F1` | (3,0,-16) | 0 | WB1 | |
| `WB1` | (3,0,-12) | 0 | WB0 | |
| `WB0` | (3,0,-7) | 0 | `<CarSpot мойки>` | |
| `WX` | (3,0,4) | 0 | T1 | |
| `T1` | (3,0,8) | 90 | T2 | |
| `T2` | (11,0,8) | 90 | `<CarSpot шлагбаума 2>`, T3 | |
| `T3` | (30,0,8) | 180 | D1 | |
| `D1` | (30,0,-3) | 180 | D2 | **J** |
| `D2` | (30,0,-24) | 90 | N_Exit | |
| `N_Exit` | (35,0,-24) | 90 | — | |
| `R1` | (6,0,-19) | 90 | `<CarSpot шлагбаума 1>` | |
| `B1N` | (11,0,-16) | 0 | KIN | |
| `B2S` | (11,0,-1) | 180 | KIN | |
| `KIN` | (11,0,-13) | 90 | K14 | **M** |
| `K14` | (14,0,-13) | 90 | P0, K18 | |
| `K18` | (18,0,-13) | 90 | P1, K22 | |
| `K22` | (22,0,-13) | 90 | P2, K26 | |
| `K26` | (26,0,-13) | 0 | P3 | |
| `P0` | (14,0,-8) | 0 | X14 | |
| `P1` | (18,0,-8) | 0 | X18 | |
| `P2` | (22,0,-8) | 0 | X22 | |
| `P3` | (26,0,-8) | 0 | X26 | |
| `X14` | (14,0,-3) | 90 | X18 | |
| `X18` | (18,0,-3) | 90 | X22 | |
| `X22` | (22,0,-3) | 90 | X26 | |
| `X26` | (26,0,-3) | 90 | D1 | |

> Места P — проездные: заезд с южной аллеи (K), выезд вперёд на северную аллею (X). `B2S` → `KIN` идёт по западной колонке x=11 мимо начала северной аллеи (она начинается с x=14) — пересечения нет.

**Точки** (по `pointId`, с `Undo`):
| Точка | pointId | serviceTypeId | Корень/столб | CarSpot (узел) | Next | WorkSpot | Стрела |
|---|---|---|---|---|---|---|---|
| Мойка | `loc1_wash_1` | wash | корень в (3,0,-2) | (3,0,-2) rot 0 | WX | локально как есть | — |
| Шлагбаум 1 | `loc1_entrance_main` (было `loc1_barrier`/`loc1_parking_exit`) | parking | столб (9,0.5,-17.5) | (11,0,-19) rot 0 | B1N | (7.5,0,-17) лицом на восток | вдоль +X, Open Axis Z |
| Шлагбаум 2 | `loc1_entrance_service` | parking | **создать копией шлагбаума 1** (`Object.Instantiate` + `Undo`), столб (9,0.5,2.5) | (11,0,4) rot 180 | B2S | (7.5,0,3) лицом на восток | вдоль +X, Open Axis Z |
- У мойки `_bufferSlots` = [WB0, WB1].
- Связи: `WB0 → CarSpot мойки`, `R1 → CarSpot ш.1`, `T2 → CarSpot ш.2`.
- `LocationLayout`: spawn, exit, queue [Q0..Q3], parking [P0..P3], mainEntrance, serviceEntrance, servicePoints [мойка].

## 5. Editor setup (пользователь) — в PR
1. `GameConfig → Traffic`: веса планов 35/35/30 (по умолчанию). `ST_Parking`: Base 2, Price Per Second 0.5.
2. Выделить `Location_1` → **Build Location 1 Roads**. Убедиться, что появился второй шлагбаум.
3. CameraRig Bounds: Min (-34,-28), Max (37,12).
4. **Bake** обоих NavMeshSurface → Ctrl+S → Play:
   - «парковка»: голова → шлагбаум 1 (встань) → +$ ≈ (2+0.5×стоянка)×тип → место → постояла → уехала через J;
   - «мойка»: при занятой мойке ждёт в буфере на съезде, очередь при этом движется к шлагбауму 1;
   - после мойки: часть уезжает по верхней дороге, часть — к шлагбауму 2 (встань) → парковка;
   - в зонах M/J по одной машине.
5. Закоммитить сцену/конфиги/.meta в ветку.

## 6. Git
Коммиты поверх 03b: `Replace parking exit with two paid entrances`, `Add service buffer and visit plans`, `Rework traffic tests for layout v3`, `Rework location layout for v3`, `Update whitebox builder to layout v3`. Push в ту же ветку.
