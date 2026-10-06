# Задача 16: Доработки Локации 2, Склада, Начисления опыта и UI колец прогресса

## ⚠️ КРИТИЧЕСКИ ВАЖНОЕ ТРЕБОВАНИЕ
**Пользователь вручную передвинул точку телепортации `TravelPlot_Travel_To_Loc1` на правильное место на Локации 2 и поднял плейн выше. ЗАПРЕЩЕНО уничтожать, сбрасывать или пересоздавать объекты Локации 2 через деструктивный ребилд! Все доработки должны вноситься аккуратно по месту (in-place) с сохранением текущих координат и настроек пользователя.**

---

## Цели задачи
1. **Точки телепортации:** Настроить спавн игрока точно на точках телепорта (на самой точке телепорта Локации 2 при перемещении из Локации 1, и обратно на точку телепорта Локации 1 при перемещении из Локации 2), связав `_targetTransform` напрямую между ними.
2. **Опыт за шлагбаум:** Настроить начисление опыта (`_xpReward`) за обслуживание машин на въездном шлакбауме (парковке).
3. **UI кольца прогресса (Dwell Ring):** Добавить над синими кругами (`ManagePadView`) Локации 2 и над кругами телепортации (`TravelPoint`) визуал заполнения кольца прогресса (точно такой же, как на Локации 1).
4. **Склад (желтый квадрат и зеленый прогресс-бар):** Добавить под точку взаимодействия со складом желтый квадрат (`WorkPad`), а над ним — зеленый прогресс-бар взятия ресурса (как на сервисах), чтобы при клике на склад игрок подходил, стоял на желтом квадрате, полоса заполнялась и только после этого выдавалась коробка.

---

## 1. Точки телепортации (Travel Points)

### Текущее состояние:
В `TravelPoint.cs` метод `TeleportPlayer()` перемещает игрока на позицию `_targetTransform.position`. Сейчас `_targetTransform` ссылается на временные пустые объекты `SpawnLoc1` и `SpawnLoc2`, которые стояли со смещением.

### Требуемые изменения:
1. В сцене `Gameplay.unity`:
   - Для точки перехода на Локации 1 (`TravelPlot_Travel_To_Loc2/TravelPoint`):
     - Назначить `_targetTransform` на точку телепорта Локации 2: `TravelPlot_Travel_To_Loc1` (или его `ApproachPoint` / дочерний `TravelPoint`).
   - Для точки перехода на Локации 2 (`TravelPlot_Travel_To_Loc1/TravelPoint`):
     - Назначить `_targetTransform` на точку телепорта Локации 1: `TravelPlot_Travel_To_Loc2` (или его `ApproachPoint` / дочерний `TravelPoint`).
   - Старые пустые объекты `SpawnLoc1` и `SpawnLoc2` можно удалить или совместить с позициями точек телепорта.
2. В коде `WhiteboxLocation2Builder.cs` (на случай будущего создания):
   - При создании `travel1To2` и `travel2To1` ссылать их `TargetTransform` непосредственно друг на друга (на их `ApproachPoint` или корневые позиции), а не на жестко закодированные мировые координаты со смещением.
   - Не трогать позиции `TravelPlot_Travel_To_Loc1`, если объект уже существует в сцене!

---

## 2. Начисление опыта за обслуживание на шлагбауме

### Проблема:
В `ST_Parking.asset` (конфиг `ServiceTypeConfig` с Id `"parking"`) поле `_xpReward` равно `0` (или не задано). При завершении обслуживания на шлагбауме вызывается `ProgressionService.OnServiceCompleted`, но из-за `type.XpReward == 0` опыт не начисляется.

### Требуемые изменения:
1. В `Assets/_Project/Configs/ServiceTypes/ST_Parking.asset`:
   - Установить `_xpReward: 5`.
2. В `Assets/_Project/Scripts/Bootstrap/Editor/Location1ConfigCreator.cs`:
   - В метод `CompleteStaffSupplies` или `FillServiceType` гарантировать, что для `"parking"` заполняется `_xpReward = 5` (а также `wash: 5`, `oil: 10`, `tires: 15`), чтобы повторный запуск конфиг-генератора не сбрасывал награду.

---

## 3. UI кольца прогресса над синими кругами и точками телепортации

### Текущее состояние:
- На Локации 1 над синими кругами `ManagePadView` висит мировой Canvas с круглой темной подложкой, белой иконкой стрелки вверх и голубым заполняющимся кругом (`DwellRingView`).
- На Локации 2 над `ManagePadView` кольцо `_ring` отсутствует (`{fileID: 0}`).
- Над точками телепортации `TravelPoint` кольцо `_ring` также отсутствует или не отображается.

### Требуемые изменения:
1. **Для синих кругов управления на Локации 2:**
   - Для каждого `ManagePadView` на Локации 2 (`Bay_loc2_tires_1/ManagePad_loc2_tires_1`, `Bay_loc2_tuning_1/ManagePad_loc2_tuning_1`, `Bay_loc2_paint_1/ManagePad_loc2_paint_1`, а также `Warehouse_loc2/ManagePad_loc2`):
     - Создать дочерний объект `Tag` (мировой Canvas, ориентированный под углом камеры, высота ~2.5м над падом).
     - По подобию `WhiteboxLocationBuilder.CreatePadCanvas`:
       - Темный круглый фон (`Background`)
       - Заполняемый круг (`Fill`) с `Image.Type = Filled`, `FillMethod = Radial360` (цвет `new Color(0.2f, 0.8f, 1f, 1f)`)
       - Иконка стрелки апгрейда (`Arrow`)
       - Компонент `DwellRingView` (поля `_fill`, `_root`, `_background`)
     - Привязать созданный `DwellRingView` к полю `_ring` на `ManagePadView`.
2. **Для точек телепортации (`TravelPoint`):**
   - Для обоих объектов `TravelPoint` (на Локации 1 и Локации 2):
     - Создать аналогичный мировой Canvas с `DwellRingView` на высоте ~2.5м над точкой телепорта (можно с иконкой перехода или чистым стильным кольцом).
     - Привязать к полю `[SerializeField] private DwellRingView _ring` в `TravelPoint.cs`.
     - При входе/клике игрока в точку телепорта `TravelPoint.Update` вызывает `_ring.Render(_dwell.Progress01)`, благодаря чему игрок наглядно видит заполнение круга перед телепортацией.

---

## 4. Склад: Желтый квадрат (WorkPad) и зеленый прогресс-бар

### Проблема:
Сейчас при клике на склад `WarehouseView.BeginInteraction()` пытается мгновенно выдать коробку через `TryBuyBoxForHungriest`. Если на Локации 2 нет боксов с дефицитом расходников или нет денег, метод завершается молча (так как `_messageLabel` не назначен). Игрок не видит никакого визуального отклика.
Пользователь требует:
> *"для склада тоже надо сделать желтый квадарт куда будет вставать игрок котоырй нажал на скалд и начал получать яшик с ресурсами и тоже нужен прогрес бар зеленый как на сервисавах когда игрок берет яшик с ресурками"*

### Требуемые изменения в коде `WarehouseView.cs`:
1. Добавить поля в `WarehouseView`:
   ```csharp
   [Header("Pickup Progress")]
   [SerializeField, Min(0.1f)]
   [Tooltip("Seconds required to pick up a box from the warehouse.")]
   private float _dwellSeconds = 1.2f;

   [SerializeField]
   [Tooltip("Parent of the green progress bar (hidden when idle).")]
   private GameObject _progressRoot;

   [SerializeField]
   [Tooltip("Horizontal fill image for pickup progress.")]
   private Image _progressBarFill;
   ```
2. Обновить логику взаимодействия:
   - Ввести состояние `private bool _isInteracting;` и таймер `private float _dwellTimer;`.
   - В `BeginInteraction()`:
     - Проверить `if (!IsInteractable) return;`
     - Если руки заняты (`_carry.HasBox`): `ShowMessage(_handsFullText); return;`
     - Проверить, нужен ли кому-то ящик: `if (_supplies.FindHungriest(_locationId) == null) { ShowMessage(_allStockedText); return; }`
     - Начать процесс забора ресурса:
       ```csharp
       _isInteracting = true;
       _dwellTimer = 0f;
       if (_progressRoot != null) _progressRoot.SetActive(true);
       if (_progressBarFill != null) _progressBarFill.fillAmount = 0f;
       ```
   - В `Update()`:
     - Если `_isInteracting`:
       - `_dwellTimer += Time.deltaTime;`
       - `float progress = Mathf.Clamp01(_dwellTimer / _dwellSeconds);`
       - `if (_progressBarFill != null) _progressBarFill.fillAmount = progress;`
       - Когда `_dwellTimer >= _dwellSeconds`:
         - Завершить получение:
           ```csharp
           _isInteracting = false;
           if (_progressRoot != null) _progressRoot.SetActive(false);
           if (_supplies.TryBuyBoxForHungriest(_locationId, true, out SupplyBox box, out ServicePoint target))
           {
               _carry.TryPick(box);
           }
           else
           {
               ShowMessage(target == null
                   ? _allStockedText
                   : string.Format(_needFormat, MoneyFormatter.Format(_supplies.GetBoxPrice(target.Supply.SupplyTypeId))));
           }
           ```
   - В `EndInteraction()`:
     - Прервать процесс, если игрок ушел или отменил команду:
       ```csharp
       _isInteracting = false;
       _dwellTimer = 0f;
       if (_progressRoot != null) _progressRoot.SetActive(false);
       ```
   - В `OnDisable()`:
     - Сбрасывать `_isInteracting = false;` и прятать `_progressRoot`.

### Требуемые визуальные объекты на Складе (Локация 1 и Локация 2):
1. **Желтый квадрат (`WorkPad`):**
   - На уровне земли точно под точкой `ApproachPoint` склада:
     - Создать примитив Cube (размер `1.6f, 0.02f, 1.6f`, позиция `ApproachPoint.position.x, 0.02f, ApproachPoint.position.z`).
     - Назначить материал `M_WorkPad` (ярко-желтый).
     - Убрать коллайдер, чтобы не блокировал клики и NavMesh.
2. **Зеленый прогресс-бар (`ServicePointHud`-стиль):**
   - Над `ApproachPoint` склада (высота ~2.4м, мировой Canvas, наклоненный к камере под фиксированным углом):
     - Темная подложка фона (`BarBackground`, цвет `new Color(0.12f, 0.14f, 0.18f, 0.9f)`).
     - Зеленый заполняющийся бар (`Fill`, `Image.Type = Filled`, горизонтальное заполнение слева направо, цвет `new Color(0.2f, 0.8f, 0.3f, 1f)`).
     - Родительский объект бара привязать к `_progressRoot`, а картинку к `_progressBarFill`.
     - По умолчанию `_progressRoot` выключен (`SetActive(false)`).
3. **Текстовое сообщение об ошибке (`Message`):**
   - Создать и привязать к `_messageLabel` компонент `TextMeshProUGUI` над складом, чтобы сообщения "Hands full", "All stocked", "Need $50" реально отображались игроку.

---

## 5. Автоматизированный Editor-скрипт обновления сцены

Создать Editor метод меню (например, в новом или существующем файле `Location2SceneUpdater.cs`):
`[MenuItem("AutoService/Setup/Apply Location 2 and Warehouse Fixes")]`

Этот метод должен выполнять все описанные настройки **недеструктивно**:
1. Открыть сцену `Gameplay.unity` (если не открыта).
2. Найти существующие объекты `TravelPlot_Travel_To_Loc2` и `TravelPlot_Travel_To_Loc1`, соединить их `TravelPoint._targetTransform` напрямую между собой (сохраняя пользовательские координаты!).
3. Создать и подключить `DwellRingView` над обоими `TravelPoint`.
4. Найти все `ManagePadView` на Локации 2 (включая склад Локации 2) и добавить им `DwellRingView`, если его нет.
5. Найти оба склада (`Warehouse_loc1` и `Warehouse_loc2`):
   - Добавить желтый `WorkPad` под `ApproachPoint`.
   - Добавить мировой Canvas с зеленым прогресс-баром и назначить в `_progressRoot` и `_progressBarFill`.
   - Назначить `_messageLabel`.
6. Обновить `ST_Parking.asset` (установить `_xpReward = 5`).
7. Сохранить сцену `EditorSceneManager.SaveScene(scene)` и ассеты `AssetDatabase.SaveAssets()`.

---

## 6. Порядок проверки

1. Запустить в Unity: **`AutoService -> Setup -> Apply Location 2 and Warehouse Fixes`**.
2. Запустить Play Mode:
   - Обслужить машину на въездном шлагбауме Локации 1 -> убедиться, что дается **5 XP** и полоса прогресса уровня в HUD заполняется.
   - Подойти к точке телепорта на Локации 1 -> встать на нее -> убедиться, что над ней наглядно **заполняется круговой прогресс Dwell**.
   - Дождаться телепортации -> убедиться, что игрок появляется **точно на точке телепорта Локации 2** (на правильном месте, выбранном пользователем).
   - Подойти к синим кругам управления боксов Локации 2 -> встать в синий круг -> убедиться, что над ним **заполняется синий круговой прогресс Dwell** и открывается окно найма/улучшения.
   - Нажать на Склад Локации 2:
     - Игрок идет и встает на **желтый квадрат** перед складом.
     - Над точкой появляется **зеленый прогресс-бар**, который плавно заполняется.
     - После заполнения шкалы игрок берет коробку в руки.
     - Если руки заняты или все боксы заполнены, над складом появляется понятный текст ("Hands full" / "All stocked").
   - Отнести ящик в бокс шиномонтажа/тюнинга, обслужить машину на желтом квадрате бокса.
   - Встать на точку телепорта Локации 2 -> дождаться заполнения круга -> убедиться, что игрок телепортируется **точно на точку телепорта Локации 1**.
