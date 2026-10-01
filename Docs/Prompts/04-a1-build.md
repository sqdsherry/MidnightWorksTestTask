# Промпт 04 (A1) — Строительство: ряд боксов, расширение парковки, рост потока

**Ветка:** `feature/04-build` (от `main` после мержа 03b) · **PR:** в `main`
**Перед началом:** `CLAUDE.md`, `Docs/GDD.md` §3 (v3), §6, §8.2–8.3, `Docs/TDD.md` §4.11, `Docs/ARCHITECTURE.md` §8.

---

## 1. Цель

Игрок тратит деньги на развитие первой локации:
- **4 проездных бокса в ряд**, у каждого **своя полоса с буфером на 2** от общей сервисной улицы. На старте построена только `Мойка 1`; `Мойка 2`, `Масло`, `Шины` — призраки.
- **Парковка**: на старте 2 места (P0, P1), ещё 2 (P2, P3) — покупаются.
- **Рост потока**: каждая постройка добавляет свой `FlowBonus` к частоте машин.
- **Призрак** на сцене (полупрозрачная копия + ценник в мире) → клик → **персонаж подходит** к его точке подхода → **стоит 1.5 с** (кольцо прогресса над призраком) → открывается **панель рядом с объектом** (название, описание, цена, кнопка «Build») → покупка → анимация появления → объект работает. Отошёл до конца — прогресс **быстро убывает** (×3 скорости), панель закрывается. Это тот же язык «подойди и постой», что у точек.

Требование уровня в A1 **заглушено** (`IUnlockGate`, всегда true) — настоящая проверка придёт в модуле прогрессии без изменения кода стройки.

```
 верхняя дорога (z=8) ─────────────────────────────────────────────────►
   ↑ (Z_T)       ↑ (Z_T)       ↑ (Z_T)       ↑
[Шины]         [Масло]       [Мойка 2]     [Мойка 1]         ← ряд боксов (z=-2)
 x=-21          x=-15         x=-9          x=-3
   ↑ буфер 2     ↑ буфер 2     ↑ буфер 2     ↑ буфер 2       ← полосы (z -11, -7)
   └─────────────┴─────────────┴──── сервисная улица z=-13 ◄── F1 ◄── Q0
```

## 2. Domain / Services

### 2.1 `Domain/Building/`
- `BuildableKind`: `ServicePoint`, `ParkingSlot`.
- `BuildPlotDefinition` (неизм.): `string Id`, `BuildableKind Kind`, `string TargetId` (pointId или индекс места строкой — см. ниже), `Money Cost`, `int RequiredLevel`, `double FlowBonus`.
- `BuildPlot` (сущность): `Definition`, `bool IsBuilt`, `void MarkBuilt()` (повторно → `InvalidOperationException`).

### 2.2 `Services/Building/`
```csharp
public interface IUnlockGate { bool IsUnlocked(int requiredLevel); event Action Changed; }
// A1: AlwaysUnlockedGate : IUnlockGate (Services). Модуль прогрессии подменит реализацию в EntryPoint.

public interface IBuildService
{
    IReadOnlyList<BuildPlot> Plots { get; }
    bool TryGet(string plotId, out BuildPlot plot);
    BuildAvailability GetAvailability(string plotId);   // Built | Locked | NotEnoughMoney | Available
    bool TryBuild(string plotId);                      // Available → wallet.TrySpend → MarkBuilt → Built event
    event Action<BuildPlot> Built;
    IReadOnlyList<string> BuiltPlotIds { get; }        // для сейва (модуль B)
    void RestoreBuilt(IEnumerable<string> plotIds);    // для сейва: без оплаты и без анимации-события «Built»? → вызывает BuiltRestored
    event Action<BuildPlot> BuiltRestored;
}
```
`BuildService` (ctor: `IWalletService`, `IUnlockGate`, `IEventBus`); `Register(BuildPlotDefinition)` из EntryPoint; публикует `BuildCompletedEvent { PlotId; Kind; TargetId }`.

### 2.3 Связь со строительством (`Services/Building/BuildEffects` или в `LocationTraffic`)
- `ServicePoint` у построенного участка регистрируется в `IServicePointService` **в момент постройки** (до этого его нет в сервисе — трафик о нём не знает).
- **`LocationTraffic`** — новые методы (закрыть `TODO(05-build)`):
  - `AddServicePoint(ServicePoint point, int bufferCapacity)` — подписка на события, буфер точки, тип услуги в список запрашиваемых;
  - `SetParkingCapacity(int capacity)` → `ParkingLot.SetCapacity` (только рост; используются слоты 0..capacity-1 из layout);
  - `SetFlowMultiplier(double multiplier)` → интервал спавна = `SpawnInterval / multiplier` (jitter как сейчас).
- `FlowMultiplier = 1 + Σ FlowBonus построенных участков` (стартовые постройки без участка — бонус 0). Считает EntryPoint/связующий класс при `Built`.

### 2.4 Конфиг — `Infrastructure/Config/BuildableConfig : ScriptableObject` (`"AutoService/Buildable"`)
`_id`, `_displayName`, `_description` (TextArea), `_kind`, `_cost`, `_requiredLevel`, `_flowBonus`. В `GameConfig` — массив `_buildables`. `IConfigProvider.Buildables` + `TryGetBuildable(id)` → `BuildableSettings` (Services, неизм.).

### 2.5 Тесты
`BuildServiceTests`: нет денег → `NotEnoughMoney` и деньги не списаны; успех → списание, `Built`, `BuildCompletedEvent`; повторная постройка → false; `Locked` при заглушке, возвращающей false (фейковый gate); `RestoreBuilt` без списания. `LocationTrafficTests`: `AddServicePoint` → машины начинают запрашивать новый тип; буфер новой точки работает; `SetParkingCapacity(3)` → третье место используется; `SetFlowMultiplier(2)` → спавн вдвое чаще (фейковый random с нулевым jitter).

## 3. Presentation

### 3.1 «Постой, чтобы открыть» — `Presentation/Interaction/DwellProgress`
Призрак — обычный **`IInteractable`** (как точки): клик → персонаж идёт к `ApproachPoint` → `BeginInteraction`.
- **`DwellProgress`** (C#-класс, переиспользуемый — пригодится панели точки в A2 и инцидентам): `DwellProgress(float duration, float decayMultiplier = 3f)`, `bool IsDwelling`, `float Progress01`, `void Begin()`, `void End()`, `bool Tick(float dt)` — true **один раз** при достижении 1; пока `!IsDwelling` — прогресс убывает со скоростью `decayMultiplier`. Чистый C# (без Unity) → положи в `Services/Core` или `Domain/Common` и покрой тестами.
- **`DwellRingView : MonoBehaviour`** — world-space кольцо (Image Filled Radial360) над объектом: `Render(float progress01)`; при 0 — скрыто.
- Длительность — `[SerializeField] float _dwellSeconds = 1.5f` на `BuildPlotView`.
- `EndInteraction` (игрок ушёл/новый клик) → `End()` + закрыть панель этого участка.

### 3.2 `Presentation/Building/`
- **`BuildPlotView : MonoBehaviour, IInteractable`**: `[SerializeField] string _plotId` (= id `BuildableConfig`), `GameObject _ghost` (полупрозрачная копия), `GameObject _target` (реальный объект, неактивен до постройки), `Transform _approachPoint`, `Transform _panelAnchor`, `InteractableHighlight _highlight`, `DwellRingView _ring`, `TMP_Text _priceTag` (world-space ценник «Oil Change · $500»), `float _dwellSeconds = 1.5f`. `IsInteractable` = ещё не построен. События `DwellCompleted`, `Left` (для презентера панели).
  - `SetBuilt(bool animate)`: прячет призрак, включает `_target`; при `animate` — «вырастание»: масштаб 0 → 1 по `AnimationCurve` с отскоком за 0.5 с (корутина, unscaled) + опциональный `ParticleSystem _buildFx`.
  - Коллайдер призрака — на слое Interactable (как у точек).
- **`BuildPanelView : MonoBehaviour`** (screen-space, один на сцену): `TMP_Text _title, _description, _cost, _requirement`, `Button _buildButton`, `TMP_Text _buildButtonLabel`, `Button _closeButton`, `RectTransform _root`. `Show(...)`, `Hide()`, `SetAffordable(bool, string label)`, `SetScreenPosition(Vector2)`; события `BuildClicked`, `CloseClicked`.
- **`BuildPanelPresenter : ITickable, IDisposable`** (ctor: `IBuildService`, `IConfigProvider`, `IWalletService`, `BuildPanelView`, `Camera`, список `BuildPlotView`):
  - тикает `DwellProgress` участка, у которого стоит игрок, обновляет его кольцо; `DwellCompleted` → заполнить панель из `BuildableSettings` и показать; `Left`/`CloseClicked`/`Built` этого участка/Esc → скрыть.
  - Каждый тик пока открыта: `camera.WorldToScreenPoint(anchor)` → позиция панели **рядом с объектом** (смещение вправо-вверх), **зажата в границах экрана**; за камерой (z<0) → скрыть.
  - Подписка на `BalanceChanged` → `SetAffordable` («Build $500» / «Need $500»). Кнопка → `TryBuild`.
- Строки UI — английские, из конфига (`DisplayName`, `Description`).

### 3.3 Связка постройки и сцены — `Presentation/Building/BuildableBinder` (C#, IDisposable)
На `IBuildService.Built`/`BuiltRestored`: найти `BuildPlotView` по id → `SetBuilt(animate: Built)` → по `Kind`:
- `ServicePoint`: `ServicePointView` внутри `_target` → зарегистрировать точку (`ServicePointService.Register` + `view.Construct` + `ServicePointPresenter`) → `LocationTraffic.AddServicePoint(point, view.BufferSlots.Length)`;
- `ParkingSlot`: `LocationTraffic.SetParkingCapacity(текущая + 1)` (TargetId = индекс места; места строятся по порядку — валидация в конфиге/ layout).
- Пересчитать `FlowMultiplier`.

### 3.4 `LocationLayout`
- `_servicePoints` содержит **все** точки (и недостроенные). Точка считается построенной на старте, если она **не** является `_target` ни одного `BuildPlotView`.
- `_parkingSlots` — все 4; стартовая ёмкость парковки = число мест, не закрытых участком (`BuildPlotView` с `Kind = ParkingSlot`).
- Валидация: каждый участок ссылается на существующую точку/место; id участков уникальны и есть в `GameConfig`.

## 4. Интеграция — `GameplayEntryPoint`
Новый шаг `RegisterBuilding()` после трафика: `BuildService` (+`AlwaysUnlockedGate`), регистрация участков из всех `BuildPlotView` локации (по их `BuildableSettings`), стартовые точки — как сейчас (только построенные), `BuildableBinder`, `BuildPanelPresenter`. Поля: `BuildPanelView _buildPanel`.

## 5. Editor-инструменты (`Bootstrap/Editor/`)

### 5.1 `AutoService → Whitebox → Create Location 1 Configs`
Создаёт **недостающие** ассеты (не трогает существующие) и вносит их в `GameConfig`:
- `ServiceTypes/ST_Oil` (Service, `oil`, $25, 9 с), `ST_Tires` (`tires`, $35, 12 с).
- `Buildables/`: `B_Wash2` (ServicePoint → `loc1_wash_2`, $400, ур. 2, flow 0.2), `B_Oil` (→ `loc1_oil`, $500, ур. 3, 0.2), `B_Tires` (→ `loc1_tires`, $900, ур. 4, 0.2), `B_Parking3` (ParkingSlot → `2`, $200, ур. 2, 0.05), `B_Parking4` (→ `3`, $350, ур. 3, 0.05). Описания — короткие английские («Second wash bay: serve two cars at once.»).

### 5.2 `Build Location 1 Roads` → таблица **v3.1** (заменяет v3)
Отличия от v3: ряд из 4 боксов, сервисная улица, верхняя дорога продлена на запад, зоны слияния `Z_T*` на верхней дороге, призраки.

**Поверхности** (слой Road): `Surface_Road` (0,0,-21.5)/(72,0.1,8); `Surface_ServiceArea` (-12,0,-4)/(26,0.1,29) — сервисная улица, полосы, боксы, выезды; `Surface_TopRoad` (5,0,8)/(58,0.1,5); `Surface_Entrance2Lane` (11,0,3)/(4,0.1,6); `Surface_Parking` (19,0,-8.25)/(20,0.1,18.5); `Surface_ExitRoad` (30,0,-8)/(5,0.1,34).

**Узлы** — как v3, кроме участка сервисной зоны:
| Узел | Position | Rot | Next | Зона |
|---|---|---|---|---|
| `F1` | (-3,0,-16) | 0 | SSm3 | |
| `SSm3` | (-3,0,-13) | 270 | Lm3_B1, SSm9 | |
| `SSm9` | (-9,0,-13) | 270 | Lm9_B1, SSm15 | |
| `SSm15` | (-15,0,-13) | 270 | Lm15_B1, SSm21 | |
| `SSm21` | (-21,0,-13) | 0 | Lm21_B1 | |
| `L{x}_B1` | (x,0,-11) | 0 | L{x}_B0 | |
| `L{x}_B0` | (x,0,-7) | 0 | `<CarSpot бокса x>` | |
| `WX{x}` | (x,0,4) | 0 | T{x} | |
| `T-21` | (-21,0,8) | 90 | T-15 | |
| `T-15` | (-15,0,8) | 90 | T-9 | **Z_T-15** |
| `T-9` | (-9,0,8) | 90 | T-3 | **Z_T-9** |
| `T-3` | (-3,0,8) | 90 | T2 | **Z_T-3** |
| `T2` … дальше как v3 | | | | |
(x ∈ {-3, -9, -15, -21}; `Lm3_B1/B0` = бывшие `WB1/WB0`, `T-3` = бывший `T1`; CarSpot бокса x → `WX{x}`. Очередь, шлагбаумы и парковка — как в v3 после правки: Q0..Q3 = x -9/-15/-21/-27, спавн (-34,0,-19), CarSpot шлагбаума 1 (11,0,-20.5), шлагбаума 2 (11,0,5.5).)

**Боксы** (корни в (x,0,-2), CarSpot в (x,0,-2) rot 0, WorkSpot локально как у мойки 1, `_bufferSlots` = [L{x}_B0, L{x}_B1]):
| x | pointId | serviceTypeId | Участок |
|---|---|---|---|
| -3 | `loc1_wash_1` | wash | — (построен) |
| -9 | `loc1_wash_2` | wash | `B_Wash2` |
| -15 | `loc1_oil` | oil | `B_Oil` |
| -21 | `loc1_tires` | tires | `B_Tires` |
- Недостроенные боксы: **копия мойки 1** (`Instantiate` + Undo), id/тип — из таблицы, объект = `_target` (неактивен); рядом — `Ghost_{id}`: копия **без скриптов и коллайдеров на детях**, материал `M_Ghost` (создать, URP Lit Transparent, белый α 0.35), BoxCollider на корне (слой Interactable) + `BuildPlotView` + `InteractableHighlight` + `ApproachPoint` (там же, где WorkSpot бокса) + world-space Canvas над крышей с ценником (TMP) и `DwellRingView` (Image, Filled Radial 360, спрайт `UISprite`/`Knob` из built-in).
- **Места парковки**: P2, P3 — `Ghost_P2/P3`: плоский куб 2.2×0.05×4.4 с `M_Ghost` + BoxCollider + `BuildPlotView` (`B_Parking3`/`B_Parking4`) + `ApproachPoint` (2 м южнее места, лицом к нему) + Canvas с ценником и кольцом.
- После пересборки стрел шлагбаумов добавлять новый `Arm` в `InteractableHighlight._renderers` шлагбаума (сейчас ссылка теряется — TDD K3).
- Повторный запуск: удаляет `Roads_v3*`, сгенерированные боксы/призраки (по маркерному компоненту `WhiteboxGenerated` — добавь пустой MonoBehaviour-маркер в Editor-only или Presentation) и строит заново.

## 6. Editor setup (пользователь) — в PR
1. **AutoService → Whitebox → Create Location 1 Configs** → проверить `GameConfig` (Service Types: 4, Buildables: 5).
2. Выделить `Location_1` → **Build Location 1 Roads** → в сцене 4 бокса (3 призрака), 2 призрака мест парковки, ценники.
3. Сцена: `ScreenHud` → добавить `BuildPanel` (Panel 360×220: Title, Description, Cost, Requirement, кнопки Build/Close; TMP) + `BuildPanelView`; назначить в `[EntryPoint]`.
4. **Bake** обоих NavMeshSurface → Ctrl+S → Play:
   - клик по призраку → персонаж подходит, над призраком заполняется кольцо (1.5 с) → рядом с объектом панель «Oil Change · $500 · Build»; денег мало → «Need $500», кнопка неактивна;
   - ушёл раньше — кольцо быстро убывает; ушёл от открытой панели — панель закрывается;
   - камеру двигаешь — панель следует за объектом и не вылезает за экран; Esc — закрывается;
   - накопить → Build → бокс «вырастает», машины начинают приезжать на новую услугу, поток чаще;
   - купить место P2 → на парковку встаёт третья машина.
5. Закоммитить сцену/конфиги/материал/.meta в ветку.

## 7. Критерии приёмки
- [ ] Тесты зелёные; Domain/Services без Unity; без аллокаций в тиках (позиционирование панели — без аллокаций).
- [ ] Новая услуга/место добавляются **только данными** (конфиг + разметка) — проверка масштабируемости.
- [ ] Сценарий §6.4 проходит.

## 8. Git
Коммиты: `Add build plot domain and build service`, `Add buildable config`, `Add dynamic points, parking capacity and flow multiplier to traffic`, `Add build tests`, `Add selectable click without walking`, `Add build plot view, panel and binder`, `Wire building into GameplayEntryPoint`, `Add config creator and v3.1 layout builder`. PR `04: Build (A1)` + Editor setup.
