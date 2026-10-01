# Промпт 02 — Input, камера, персонаж (click-to-move), взаимодействие

**Ветка:** `feature/02-player` (от актуального `main`) · **PR:** в `main`
**Перед началом:** прочитай `CLAUDE.md`, `Docs/TDD.md` (§0, §3, §4.6, §4.17, §4.18), `Docs/GDD.md` §4.

---

## 1. Контекст и цель

Модуль 01 (Core) смержен: есть `ServiceContainer`, `GameLoop`, `IEventBus`, `IPauseService`, `GameplayEntryPoint`.

Сейчас делаем **управление игроком** на whitebox-сцене (кубы вместо ассетов):
- игрок кликает ЛКМ по **полу** → персонаж идёт туда по NavMesh;
- кликает по **интерактивному объекту** → персонаж идёт к его точке подхода (`ApproachPoint`), разворачивается и **начинает взаимодействие**; новый клик **прерывает** взаимодействие;
- наведение курсора подсвечивает интерактивный объект;
- камера в изометрии следует за персонажем, WASD / край экрана — ручной сдвиг, колесо — зум, Space — вернуть к персонажу.

Это фундамент для модуля 03: там точки услуг (шлагбаум, мойка) станут `IInteractable`, а «начало взаимодействия» = «игрок занял рабочее место точки». **Игровой логики точек здесь нет** — только движение, наведение и контракт взаимодействия.

Весь код этого модуля — в слое **Presentation** (Unity-типы: `Vector3`, `NavMeshAgent`, Input System). Domain/Services не меняются.

## 2. Правила

Все правила `CLAUDE.md` действуют. Дополнительно для модуля:
- Ввод — только через **Input System** (пакет уже стоит), без старого `UnityEngine.Input`.
- Ввод читается **событиями** (`performed`) и опросом в `Tick` — никаких `Update()` в классах ввода/персонажа. Исключение: `CameraRig.LateUpdate` (камера должна двигаться после агента в том же кадре, иначе дрожание) — пометить `// Why:`.
- Raycast'ы — только `Physics.RaycastNonAlloc` с заранее выделенным буфером.
- Подсветка — через `MaterialPropertyBlock` (не создавать копии материалов).
- Никаких `Camera.main` в рантайме — камера передаётся ссылкой.

## 3. Ассет ввода

Создай текстовый файл `Assets/_Project/Settings/Input/GameControls.inputactions` (JSON формата Input System, без `.meta`), action map **`Gameplay`**:

| Action | Type | Bindings |
|---|---|---|
| `Point` | Value / Vector2 | `<Mouse>/position` |
| `Click` | Button | `<Mouse>/leftButton` |
| `Pan` | Value / Vector2 | 2D Vector composite: W/S/A/D + Up/Down/Left/Right arrows |
| `Zoom` | Value / Axis | `<Mouse>/scroll/y` |
| `Recenter` | Button | `<Keyboard>/space` |
| `Cancel` | Button | `<Keyboard>/escape` |

Генерацию C#-класса **не** включаем — ассет передаётся ссылкой `InputActionAsset`, экшены ищутся по имени один раз.

## 4. Контракты (namespace `AutoService.Presentation.*`)

### 4.1 Input — `Presentation/Controls/`

> Namespace `AutoService.Presentation.Controls`, не `Input` (перекрыло бы `UnityEngine.Input`) и не `PlayerInput` (совпадает с компонентом Input System).

**`GameplayInput : IDisposable`** (чистый C#-класс, обёртка над ассетом)
```csharp
public GameplayInput(InputActionAsset asset);   // находит map "Gameplay" и все экшены; нет экшена → InvalidOperationException с именем
public Vector2 PointerPosition { get; }          // экранные пиксели
public Vector2 Pan { get; }                      // -1..1 по осям
public float Zoom { get; }                       // сырое значение колеса за кадр (+ вверх)
public event Action Clicked;                     // ЛКМ нажата
public event Action RecenterPressed;
public event Action CancelPressed;
public void Enable();
public void Disable();
public void Dispose();                           // отписка от performed + Disable
```

### 4.2 Interaction — `Presentation/Interaction/`

**`IInteractable`** — всё, к чему персонаж может подойти и с чем взаимодействовать.
```csharp
/// World position the character walks to.
Vector3 ApproachPosition { get; }
/// Rotation the character takes after arriving (faces the object).
Quaternion ApproachRotation { get; }
/// False → clicks on it are ignored (e.g. locked/unbuilt later).
bool IsInteractable { get; }
/// Hover feedback.
void SetHighlighted(bool highlighted);
/// The character arrived and starts interacting (module 03: occupies the work spot).
void BeginInteraction();
/// The character left (new command) — interaction ends.
void EndInteraction();
```

**`InteractableHighlight : MonoBehaviour`** — переиспользуемая подсветка.
- `[SerializeField] Renderer[] _renderers`, `[SerializeField] Color _highlightColor`, `[SerializeField, Range(0,1)] float _intensity`.
- `public void SetHighlighted(bool)` — через **один** закешированный `MaterialPropertyBlock` выставляет `_EmissionColor` (URP Lit) или тинт `_BaseColor`; выключение — `renderer.SetPropertyBlock(null)`. Выбор свойства задокументировать.

**`DebugInteractable : MonoBehaviour, IInteractable`** — тестовый объект для whitebox (будет полезен и дальше для отладки).
- `[SerializeField] Transform _approachPoint;` `[SerializeField] InteractableHighlight _highlight;` `[SerializeField] bool _isInteractable = true;`
- `BeginInteraction/EndInteraction` → лог через `Debug.Log` с именем объекта (Presentation может логировать в Unity) и смена цвета подсветки на «занято», чтобы было видно в Game View.
- `OnDrawGizmos` — сфера + стрелка в `_approachPoint`.

**`PointerRaycaster`** (чистый C#-класс)
```csharp
public PointerRaycaster(Camera camera, LayerMask interactableMask, LayerMask groundMask, float maxDistance = 200f);
public bool TryGetInteractable(Vector2 screenPosition, out IInteractable interactable);
public bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point);
```
- `RaycastNonAlloc` в буфер на 8 хитов, берётся ближайший.
- `IInteractable` ищется через `collider.GetComponentInParent<IInteractable>()` **только когда сменился коллайдер** (кеш последнего коллайдера → результата), чтобы hover каждый кадр не дёргал поиск.
- Клик сквозь UI отсекается не здесь, а в `PlayerInputPresenter`.

### 4.3 Player — `Presentation/Player/`

**`PlayerView : MonoBehaviour`** — персонаж.
- `[SerializeField] NavMeshAgent _agent;` `[SerializeField] float _arrivalTolerance = 0.15f;` `[SerializeField] float _turnSpeed = 720f;` (°/с, разворот к `ApproachRotation` после прибытия)
- API:
```csharp
public bool IsMoving { get; }                       // для будущей анимации
public IInteractable CurrentTarget { get; }         // к кому идёт / с кем взаимодействует
public bool IsInteracting { get; }
public event Action<IInteractable> InteractionStarted;
public event Action<IInteractable> InteractionEnded;
public void MoveTo(Vector3 worldPosition);           // прерывает текущее взаимодействие
public void ApproachAndInteract(IInteractable target); // тот же target, с которым уже взаимодействует → no-op
public void Stop();                                  // прерывает всё, остаётся на месте
```
- Позиции приводятся к NavMesh через `NavMesh.SamplePosition` (радиус ~1 м); не нашлось → команда игнорируется.
- Прибытие и поворот проверяются в **`PlayerMotor : ITickable`** (см. ниже), а не в `Update`.
- Правила прерывания: любая новая команда сначала вызывает `EndInteraction()` у текущей цели (если взаимодействие уже началось) и `InteractionEnded`.

**`PlayerMotor : ITickable`** (чистый C#-класс, ctor: `PlayerView`)
- Каждый тик: если идём к цели и `!agent.pathPending && agent.remainingDistance <= stoppingDistance + tolerance` → прибыли → плавный поворот к `ApproachRotation` (`Quaternion.RotateTowards`, `deltaTime`) → по завершении поворота `target.BeginInteraction()` и `InteractionStarted`.
- Реализуй как маленькую FSM внутри (`Idle / MovingToPoint / MovingToTarget / Turning / Interacting`) — состояние enum, переходы в одном месте.
- Если агент «застрял» (путь невалиден / `PathPartial`) — остановиться в `Idle`, предупреждение в лог.

> Why `PlayerView` + `PlayerMotor` раздельно: `PlayerView` — Unity-компонент со ссылками, `PlayerMotor` — логика тика через `GameLoop`, без `Update`.

**`PlayerInputPresenter : ITickable, IDisposable`** (чистый C#; ctor: `GameplayInput`, `PointerRaycaster`, `PlayerView`, `IPauseService`, `ClickMarkerView` (может быть null))
- На `Clicked`: если пауза или курсор над UI (`EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()`) → игнор. Иначе: interactable под курсором и `IsInteractable` → `ApproachAndInteract`; иначе точка пола → `MoveTo` + `ClickMarkerView.Show(point)`.
- В `Tick`: hover — подсветка объекта под курсором; при смене снять со старого (`SetHighlighted(false)`), поставить на новый. На паузе/над UI — снять подсветку.
- `Dispose` — отписки.

**`ClickMarkerView : MonoBehaviour`** — маркер клика по полу (juice).
- Один переиспользуемый объект (кольцо/диск); `Show(Vector3)` → перемещается в точку, анимация масштаба `AnimationCurve` + исчезновение за ~0.4 с через корутину на **unscaled** времени. Повторный `Show` перезапускает анимацию (останавливать прошлую корутину). Кешировать `WaitForEndOfFrame`/не аллоцировать в цикле.

### 4.4 Camera — `Presentation/CameraControl/`

> Namespace `AutoService.Presentation.CameraControl`, не `Camera`: иначе внутри `AutoService.Presentation.*` имя `Camera` перекроет `UnityEngine.Camera` (см. ловушки имён в `CLAUDE.md`).

**`CameraRig : MonoBehaviour`** — изометрическая камера.
- Иерархия (собирает пользователь): `CameraRig` (pivot на земле) → дочерняя `Main Camera`.
- `[SerializeField]`: `Transform _cameraTransform`, `float _pitch = 50f`, `float _yaw = 45f`, `float _distance = 18f`, `Vector2 _distanceRange = (10, 28)`, `float _zoomStep = 2f`, `float _zoomSmoothing = 10f`, `float _followSmoothing = 8f`, `float _panSpeed = 15f`, `bool _edgePanEnabled = true`, `float _edgePanMargin = 12f` (px), `Vector2 _boundsMin/_boundsMax` (XZ-границы pivot).
- `public void Construct(GameplayInput input, Transform followTarget)`.
- `public void Recenter()` — снова следовать за целью.
- `public void SetBounds(Vector2 min, Vector2 max)` — для переключения локаций в модуле 11.
- `LateUpdate` (единственное исключение, `// Why:`): **unscaled** delta;
  - есть `Pan` с клавиатуры или курсор в зоне края экрана (только если `Application.isFocused`) → режим **ручной**: двигаем pivot по XZ в плоскости камеры (с учётом `_yaw`), follow отключается;
  - `RecenterPressed` или новый `MoveTo`/`ApproachAndInteract` → вернуть follow (подпишись на события `PlayerView`/`GameplayInput`, без опроса);
  - follow → pivot к позиции цели с экспоненциальным сглаживанием `1 - exp(-k*dt)`;
  - зум → целевая дистанция по шагам колеса, плавно, в `_distanceRange`;
  - pivot ограничен границами; камера = pivot + `Quaternion.Euler(_pitch, _yaw, 0) * (Vector3.back * distance)`, смотрит на pivot.
- `OnDrawGizmosSelected` — прямоугольник границ.

### 4.5 Интеграция в `GameplayEntryPoint`

Добавь `[SerializeField]`-поля, сгруппированные `[Header("Player & Input")]`:
`InputActionAsset _inputActions`, `Camera _camera`, `CameraRig _cameraRig`, `PlayerView _player`, `ClickMarkerView _clickMarker`, `LayerMask _interactableMask`, `LayerMask _groundMask`.

Новый шаг **`RegisterPlayer()`** (вызывать после `RegisterEconomy`):
1. `var input = new GameplayInput(_inputActions)` — зарегистрировать (для `Dispose`), `input.Enable()`.
2. `var raycaster = new PointerRaycaster(_camera, _interactableMask, _groundMask)`.
3. `var motor = new PlayerMotor(_player)` → `Register` (тикается).
4. `var presenter = new PlayerInputPresenter(input, raycaster, _player, pause, _clickMarker)` → `Register` (тикается, диспоузится).
5. `_cameraRig.Construct(input, _player.transform)`.
6. Незаполненные обязательные ссылки → понятная ошибка через `IGameLogger` с именем поля, шаг пропускается (не NRE).

> Presentation-классы регистрируются в сценовом контейнере под своим конкретным типом (`Register(presenter)`), т.к. интерфейсов-контрактов для них нет — это нормально, они нужны контейнеру только для `Dispose`.

Bootstrap asmdef уже ссылается на Presentation; добавь `Unity.InputSystem` в references `AutoService.Bootstrap` (для типа `InputActionAsset` в полях).

## 5. Уборка

- Удали шаблонный `Assets/InputSystem_Actions.inputactions` (+ `.meta`) — **после** того как пользователь отключит его как Project-wide Actions (шаг Editor setup 1). Если удалять кодеру неудобно до этого шага — оставь пункт пользователю в Editor setup.

## 6. Editor setup (пользователь, после кода) — включи в описание PR

1. *Edit → Project Settings → Input System Package* → **Project-wide Actions = None**. Затем удалить `Assets/InputSystem_Actions.inputactions` через окно Project.
2. *Project Settings → Tags and Layers*: User Layer 6 = `Ground`, 7 = `Interactable`.
3. Сцена `Gameplay` (whitebox):
   - `Ground`: Plane, Scale (5, 1, 5) = 50×50 м, слой **Ground**, серый материал.
   - `[Navigation]`: пустой GO + компонент **NavMeshSurface** (Agent Type Humanoid, Collect Objects = All) → **Bake**.
   - `Player`: Capsule (высота 2), удалить у неё `CapsuleCollider` (чтобы raycast по полу не ловил игрока), добавить **NavMeshAgent** (Speed 6, Angular Speed 720, Acceleration 30, Stopping Distance 0.1) + **PlayerView** (поле Agent).
   - Два куба `DebugPoint_A`, `DebugPoint_B` (1.5×1.5×1.5) в ~8 м друг от друга, слой **Interactable**, яркий материал URP Lit с включённым Emission; у каждого дочерний пустой `ApproachPoint` в 1.2 м перед гранью, повернуть синей осью (Z) к кубу; компоненты **InteractableHighlight** (Renderers = куб) и **DebugInteractable** (Approach Point, Highlight). Перепечь NavMesh.
   - `ClickMarker`: Quad или тонкий Cylinder (диаметр 0.6, высота 0.02), **без коллайдера**, полупрозрачный unlit-материал + **ClickMarkerView**.
   - Камера: пустой GO `CameraRig` (0,0,0) + **CameraRig**; `Main Camera` сделать дочерней, назначить в `Camera Transform`. Bounds: (-25,-25)..(25,25).
   - Сцене нужен **EventSystem** (*GameObject → UI → Event System*) с **Input System UI Input Module** (Unity предложит заменить Standalone — согласиться).
   - `[EntryPoint]` → `GameplayEntryPoint`: назначить Input Actions = `GameControls`, Camera, Camera Rig, Player, Click Marker, Interactable Mask = Interactable, Ground Mask = Ground.
4. Play: клик по полу → идёт, маркер вспыхивает; наведение на куб → подсветка; клик по кубу → подходит, разворачивается, в Console `BeginInteraction`; клик по полу → `EndInteraction`; WASD/край экрана → камера сдвигается, Space → возвращается; колесо → зум.
5. Закоммитить в ветку: `.meta`, сцену, материалы, `GameControls.inputactions.meta`, `TagManager.asset`, изменения настроек Input System, удаление шаблонного ассета.

## 7. Критерии приёмки

- [ ] Компиляция без ошибок/warning'ов в нашем коде; в Console при Play нет ошибок.
- [ ] Нет `Update()` кроме `GameLoop` и `CameraRig.LateUpdate`; нет `Camera.main`, `FindObjectOfType`, старого `Input`.
- [ ] `RaycastNonAlloc`, `MaterialPropertyBlock`; в Profiler нет GC Alloc в кадре при простое и при наведении (проверка по возможности).
- [ ] Клик сквозь UI не двигает персонажа (проверим в модуле 09, но код уже есть).
- [ ] Всё из шага 4 Editor setup работает.
- [ ] XML-доки на публичном API, `// Why:` на неочевидном.

## 8. Git

- Ветка `feature/02-player` от свежего `main`.
- Коммиты по смыслу: `Add gameplay input actions and wrapper`, `Add interaction contracts and highlight`, `Add player view and motor`, `Add player input presenter and click marker`, `Add isometric camera rig`, `Wire player module into GameplayEntryPoint`.
- PR `02: Player, input, camera` + Editor setup из §6 + отклонения от промпта с причинами.
