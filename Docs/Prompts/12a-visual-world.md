# Промпт 12a (D1) — Визуал мира: ассеты Kenney вместо whitebox

**Ветка:** `feature/12a-visual-world` от `main` · **Папка:** основная.
**Перед началом:** `CLAUDE.md`, `Docs/GDD.md` §11 (стиль), лог решений GDD 2026-10-02 (ассеты — только Kenney, точки собираются из деталей + анимация услуги), `Docs/TZ.md` («единый визуальный стиль»), код `Bootstrap/Editor/WhiteboxLocationBuilder.cs`, `ModuleSetupA2.cs`, `Presentation/Traffic/CarView.cs`, `CarVisualCatalog`, `Presentation/Player/PlayerView.cs`, `Presentation/Staff/StaffView.cs`, `Presentation/Points/ServicePointPresenter.cs`.

> **Параллельно** пользователь пишет модуль 07 (прогрессия): Domain/Services `Progression`, `ServiceTypeConfig` / `ServiceTypeSettings`, `GameConfig`, `ProgressionInstaller`, строка gate в `BuildingInstaller`, список инсталлеров в `GameplayEntryPoint`. **Не трогать.**
>
> UI-скин (Kenney UI Pack, иконки, шрифт), звук и «+$» — **следующий модуль 12b**. Здесь только мир.

---

## 0. Источник ассетов
Паки лежат в **`<repo>/Donwload/`** (вне `Assets`, в `.gitignore`). Все CC0, в каждом `License.txt`.

| Пак | Что берём |
|---|---|
| `kenney_car-kit` | машины `sedan`, `suv`, `sedan-sports`; колёса `wheel-default` / `wheel-dark`, `debris-tire`; `cone`, `box` |
| `kenney_cityKitRoads_1.1` | `road_straight`, `road_bend`/`road_curve`, `road_crossroad`/`road_intersection`, `road_sideEntry`/`road_sideExit`, `road_drivewayDouble`/`Single`, `road_square`, `tile_low` (тротуар), `light_square`/`light_curved` (фонари) |
| `kenney_city-kit-commercial_2.1` | декор-здания вокруг (`building-a..n`), фон (`low-detail-building-*`, `building-skyscraper-*`), `detail-awning(-wide)`, `detail-overhang` (навесы будки и комнаты персонала) |
| `kenney_conveyor-kit` | **корпус боксов и склада**: `structure-doorway-wide`, `door-wide-open`, `structure-wall`, `structure-window(-wide)`, `structure-corner-*`, `top(-large)`, `floor(-large)`; `scanner-high` (арка мойки); `cover`/`cover-hopper`/`robot-arm-a` (масло / шины); `box-small/long/wide/large`, `conveyor-long` (склад); `structure-yellow-*` (ограждения) |
| `kenney_racing-kit` | `barrierRed`/`barrierWhite`, `fenceStraight`, `pylon`, `lightPostModern`, `treeLarge`/`treeSmall`, `billboard` (вывеска сервиса) |
| `kenney_cityKitSuburban` | `tree_large`/`tree_small`, `fence_*`, `path_*` — зелень и дальний фон |
| `kenney_mini-characters` | игрок + NPC (с анимациями внутри FBX: idle, walk, holding-both, interact) |

Если в паке нет нужного файла с таким именем — найди ближайший по превью (`Previews/*.png`) и напиши в отчёте, что взял.

## 1. Импорт — `AutoService/Setup/Import Kenney Assets` (`Bootstrap/Editor/KenneyImporter.cs`)
- **Копирует только выбранные файлы** (белый список в коде, §0) из `Donwload/<pack>/Models/FBX format/` в `Assets/_Project/Art/Kenney/<Pack>/`, вместе с нужными текстурами (`Textures/colormap.png`, сохраняя относительный путь — FBX ссылаются на него) и `License.txt`.
- Нет папки `Donwload` или файла → понятная ошибка с путём и что скачать.
- Повторный запуск не плодит дубли (файл есть → пропуск).
- Настройки импорта через `ModelImporter` (в коде утилиты, не `AssetPostprocessor`, чтобы не влиять на будущие ассеты):
  - `globalScale` = 1 (масштаб — в префабах-обёртках, §2);
  - `importAnimation` = только для Mini Characters;
  - `animationType` = **Generic** у Mini Characters, avatar из модели;
  - клипы `idle`, `walk`, `holding-both`, `interact` (по именам внутри FBX — проверь фактические имена, Unity покажет take-имена; если имена другие — возьми ближайшие и перечисли в отчёте) с `loopTime` для idle / walk / holding;
  - `materialImportMode` = ImportViaMaterialDescription (URP 17 сам создаёт URP Lit).
- **Единый вид материалов.** Утилита проходит по импортированным материалам и ставит Smoothness 0.1, Metallic 0, без Specular Highlights. Иначе Kenney-модели блестят по-разному.

## 2. Префабы-обёртки — `Assets/_Project/Prefabs/Art/`
Ни одна логика не ссылается на Kenney-файлы напрямую — только на наши префабы-обёртки. Так замена модели не трогает код и сцену.
- Обёртка = пустой корень + дочерняя модель, масштаб подобран **по габаритам** (`Renderer.bounds` в Editor):

  | Модель | Подгонка |
  |---|---|
  | машины | длина 4.0 (sedan), 4.3 (suv), 4.2 (sports) м, по оси Z, капотом +Z |
  | персонажи | рост 1.8 м |
  | дорожные тайлы | 1 тайл = 4×4 м |
  | Conveyor-структуры | высота стены 3 м (≈ ×1) |
  | Commercial-здания | ширина 8–12 м |

  Функция `FitToSize(GameObject, Vector3 targetSize, Axis)` в утилите. Коэффициенты печатать в лог.
- Коллайдеры моделям **не добавлять**, логика использует свои коллайдеры (клики, NavMesh).

## 3. Замена whitebox — `AutoService/Setup/Run D1 Setup` (`Bootstrap/Editor/ModuleSetupD1.cs`)
Одна кнопка, идемпотентно (маркер `WhiteboxGenerated` / свой `ArtGenerated`). **Принцип: логические объекты (точки, узлы дорог, CarSpot / WorkSpot, коллайдеры, NavMesh-поверхности) не двигаются и не удаляются; меняется только визуал.**

### 3.1 Дороги и площадки
- У поверхностей `Surface_*` (слой Road, по ним печётся NavMesh) **выключить `MeshRenderer`** (коллайдер и NavMesh остаются).
- Сверху раскладываются тайлы дорог по габаритам каждой поверхности:
  - прямые вдоль длинной оси;
  - на стыках — перекрёсток / поворот, если удобно, иначе прямые + `road_square`;
  - точность «как у whitebox» не нужна, важно, чтобы машины визуально ехали по асфальту.
- Сервисная зона и полосы к боксам — `road_square` / асфальт. Остров со складом и комнатой персонала — `tile_low` (тротуар).
- Парковка — асфальт + **белые линии мест** (тонкие квады 0.1×0.02×4.4 между местами, свой `M_LineWhite`).
- Остальная земля (плоскость `Ground`) — травяной материал `M_Grass` (`7CB342`), матовый.

### 3.2 Боксы (4 шт. + призраки)
- Бокс = conveyor-корпус вокруг CarSpot:
  - две боковые стены вдоль Z, длиной ~6 м (`structure-wall` / `-window`);
  - спереди и сзади — `structure-doorway-wide` (проём под машину ≥ 2.6 м шириной, иначе масштаб ×1.5 по X);
  - крыша `top-large`;
  - **вывеска** над въездом — TMP-текст с названием услуги из `ServiceTypeSettings.DisplayName` (ставится утилитой один раз; текст — данные сцены, не код).
- **Наполнение по типу точки** (по `serviceTypeId`, таблица в утилите):

  | Тип | Наполнение |
  |---|---|
  | `wash` | арка `scanner-high` + **две вертикальные щётки** (цилиндры ⌀0.6, высота 2, материал `M_Brush` `29B6F6`) по бокам машины + `ParticleSystem` пены (белые сферы, 30 частиц/с, unscaled off) |
  | `oil` | **подъёмник**: две плоские плиты 0.5×0.15×3 под колёсами (`M_Metal` `9E9E9E`) + 2–3 бочки (цилиндры `E65100`) у стены + `cover-hopper` |
  | `tires` | стопки шин (`wheel-dark` / `debris-tire`, по 3–4) у стен + `robot-arm-a` или `cover` |

- **Призраки стройки** и неактивные `_target` должны получить тот же визуал: применить визуал к `_target`-боксам, затем перегенерировать призраков из шаблонного бокса тем же кодом, что в `WhiteboxLocationBuilder` (вынести общий метод — не дублировать). Материал призрака — `M_Ghost` на всех рендерах копии.
- `NavMeshObstacle` (K4) пересчитать по новым стенам.
- WorkPad / ManagePad / кольца / HUD точек — **не трогать** (они логика + UI).

### 3.3 Анимация услуги — `Presentation/Points/ServiceFx` (MonoBehaviour на корне бокса, пассивный)
- `ServicePointPresenter` при `Tick` вызывает `fx.Render(state, progress, deltaTime)`. Ссылка на `ServiceFx` ищется **один раз** в конструкторе презентера (`TryGetComponent`), как `BarrierArm`.
- Время — **игровое** (`deltaTime` из тика): на паузе всё стоит.

| Вариант | Поведение |
|---|---|
| `WashFx` | пока `Servicing` — щётки вращаются (360°/с вокруг Y) и чуть «дышат» к машине (±0.2 м по синусу); пена включена; иначе выкл. |
| `LiftFx` | `Servicing` — плиты поднимаются до 0.8 м за первые 15% прогресса, держатся, опускаются за последние 15%; `CarView` **не трогать**: машина визуально стоит на месте — допустимо (опционально: дочерний «визуал» машины поднимается — только если не ломает `CarView`) |
| `TireFx` | `Servicing` — ближайшая стопка шин «подпрыгивает» по `AnimationCurve` раз в 1.5 с, `robot-arm` вращается ±30° |

- Один абстрактный базовый класс `ServiceFx : MonoBehaviour` с `abstract void Render(ServicePointState, float progress, float dt)` и три `sealed`-наследника. Без `Update`.

### 3.4 Шлагбаумы, склад, комната персонала
- **Шлагбаумы:** стойка — `structure-yellow-high` или `barrierRed`; **стрела** (логика `BarrierArm` вращает `ArmPivot`) — заменить меш стрелы на красно-белую полосатую (`M_ArmStripe` — текстура 2 полосы 64×8, генерируется утилитой в `Art/Generated/`), размеры прежние. Рядом — будка оператора: маленький `cover-window` + навес `detail-awning`. Ссылка `InteractableHighlight._renderers` — обновить на новые рендеры (урок K3).
- **Склад:** conveyor-стены и `door-wide-open` вокруг текущего куба (куб: рендер выключить, коллайдер оставить); внутри — `box-*` штабелями и `conveyor-long`; снаружи у ManagePad — пара ящиков.
- **Комната персонала:** `structure-wall` + `door` + навес `detail-overhang` + `detail-parasol-a` рядом.
- Места кладовщиков у склада — без визуала (логика).

### 3.5 Машины
- `Prefabs/Cars/Car_Sedan|Suv|Sport`: внутри у каждого дочерний `Visual` (или аналог — посмотри фактическую иерархию) → заменить кубы на обёртку модели Kenney.
- Корень, `CarView`, `NavMeshAgent` (радиус, высота) — **не трогать**.
- Цвет: Kenney-машины уже цветные — оставить; спорткар — sedan-sports.
- Если у префаба есть подписи / рендеры, на которые ссылается `CarView` — обновить ссылки.

### 3.6 Персонажи — `Presentation/Characters/CharacterAnimator` (C#, тикается, не MonoBehaviour-Update)
- Игрок — `character-male-a`; работники — `character-male-e`; кладовщики — `character-female-b`. Материал — общий colormap; различие по ролям — через модель, без перекраски.
- Модель — дочерний объект `Visual` у `Player` и в `Staff.prefab`, капсула-рендер выключен (коллайдер и агент не трогать).
- **Animator Controller** `Art/Characters/AC_Character.controller` (создаёт утилита):
  - параметры `Speed` (float), `Carrying` (bool);
  - состояния Idle / Walk / CarryIdle / CarryWalk;
  - переходы по `Speed > 0.1` и `Carrying`.
- `CharacterAnimator` (C#-класс, создаётся в инсталлерах для игрока и каждого NPC; `Tick` из `GameLoop` через презентер / `StaffAgents`):
  - `animator.SetFloat(SpeedHash, agent.velocity.magnitude)`, `SetBool(CarryingHash, …)`;
  - хеши параметров — `static readonly int` (неизменяемые — не нарушают правило о статическом состоянии);
  - `Carrying` — от `IPlayerCarry.HasBox` / `StaffMember.CarriedBox`.
- Ящик в руках — `CarrySocket` переставить к рукам модели (вперёд-вверх, ~1.0 м). Ящик — conveyor `box-small` (обёртка), цвет по `SupplyVisualCatalog` через `MaterialPropertyBlock` (не создавать материалы в рантайме).

### 3.7 Декор и свет
- По периметру локации — commercial-здания (3–5), фонари вдоль дорог, деревья и кусты кучками, забор вдоль задней стороны, пара конусов и барьеров у въезда, рекламный щит с названием игры (TMP на `billboard`). Дальний фон — 4–6 `low-detail-building`. **Ничего не ставить на дороги, полосы, парковку, острова и пути NPC.** Утилита проверяет: декор не пересекает bounds поверхностей `Surface_*` и не ближе 1.5 м к любому `RoadNode`, `WorkSpot`, `ManagePad`.
- Свет:
  - Directional Light тёплый (`FFF4E0`), угол 50°, Soft Shadows, strength 0.6;
  - Environment — Gradient (sky `A7D8FF`, equator `DDE7EE`, ground `8D9A7A`);
  - Camera background `A7D8FF`;
  - URP Volume: только Tonemapping Neutral + лёгкий Color Adjustments (saturation +10). **Bloom / SSAO не включать** (дёшево и чисто).
- После всего — **Bake NavMesh** (общий метод из A2), валидация `LocationLayout`, сохранение.

## 4. Правила
- Всё по `CLAUDE.md`.
- Рантайм-код: `ServiceFx`-наследники, `CharacterAnimator` + правка `ServicePointPresenter` / `StaffAgents` / инсталлера игрока — без аллокаций в тике, XML-доки, `// Why:`.
- Утилиты: только Editor, идемпотентны, не трогают пользовательские правки, отказываются в Play mode / во время компиляции, итоговый лог.
- Никаких новых пакетов. Никаких шейдеров на Shader Graph — только URP Lit / Unlit.

## 5. Тесты
Логику визуала тестировать не нужно. Прогнать существующие (все зелёные): правки `ServicePointPresenter` и `StaffAgents` не должны ничего сломать.

## 6. Editor setup (пользователь) — в PR
1. **AutoService → Setup → Import Kenney Assets** → без ошибок.
2. **AutoService → Setup → Run D1 Setup** → без ошибок.
3. Play:
   - дороги, тротуары, трава;
   - боксы из conveyor-деталей с вывесками;
   - машины Kenney ездят по асфальту, не проваливаются и не висят над ним;
   - на мойке крутятся щётки и идёт пена, на масле поднимается подъёмник, у шин движется стопка;
   - персонаж ходит с анимацией, с ящиком — в позе «несёт»;
   - работники и кладовщики анимированы;
   - декор не мешает ни машинам, ни игроку;
   - клики по точкам, падам и призракам работают;
   - FPS не просел.
4. Run All.

## 7. Git
Коммиты:
1. `Add Kenney importer and art wrappers`
2. `Add service fx and character animator`
3. `Add D1 setup: roads, bays, barriers, warehouse, decor, light`
4. `Swap car and character visuals`

Ассеты в `Assets/_Project/Art/Kenney/**` коммитит архитектор после Editor setup. PR `12a: World visuals (D1)`.
