# Задача 15: Финализация Локации 2, приведение к полностью рабочему виду и подключение Progression HUD

## Цель
Привести Локацию 2 и общую прогрессию игры в полностью рабочее, сбалансированное состояние:
1. Заполнить конфиги сервисов Локации 2 (`tires_loc2`, `tuning_loc2`, `paint_loc2`): расходники (`SupplyTypeId`), вместимость (`SupplyCapacity`), найм рабочих (`WorkerTitle`, `WorkerHireCost`, `WorkerRequiredLevel`), награды опыта (`XpReward`).
2. Настроить выдачу опыта (`XpReward`) для сервисов Локации 1 (`wash`, `oil`, `tires`), чтобы игрок мог прокачиваться с самого начала игры.
3. Добавить визуальные цвета коробок расходников (`SupplyVisualCatalog`) для Локации 2 (`tires_box`, `tuning_parts`, `paint_cans`).
4. Сбалансировать стоимость и требуемые уровни для участков Локации 2 (`B_Loc2_Tires1`, `B_Loc2_Tuning1`, `B_Loc2_Paint1`, `B_TravelToLoc2`).
5. Настроить биндинг тегов цен и кольца dwell для Travel Plot (`CreateTravelPlot` в `WhiteboxLocation2Builder.cs`).
6. Автоматизировать запекание NavMesh при сборке Локации 2 через `WhiteboxLocation2Builder.BuildLocation2()`.
7. Инстанциировать и подключить `ProgressionView` на HUD Canvas в сцене `Gameplay.unity` и связать с `GameplayEntryPoint._progressionView` через удобный Editor MenuItem.

---

## 1. Конфигурация сервисов Локации 2 и Локации 1

### 1.1. Обновление `Assets/_Project/Scripts/Bootstrap/Editor/Location2ConfigCreator.cs`
В `Location2ConfigCreator.cs`:
1. Расширить структуру `ServiceTypeSpec`, добавив параметры:
   - `string supplyTypeId`
   - `int supplyCapacity`
   - `string workerTitle`
   - `long workerHireCost`
   - `int workerRequiredLevel`
   - `int xpReward`
2. Обновить массив `ServiceTypes`:
   - **Tires (`tires_loc2`)**:
     - Asset: `"ST_TiresLoc2"`, Id: `"tires_loc2"`, Name: `"Tires"`, BasePrice: `35`, Duration: `12f`
     - Supply: `"tires_box"`, Capacity: `10`
     - Worker: `"Tire Specialist"`, HireCost: `500`, RequiredLevel: `3`
     - XpReward: `15`
   - **Tuning (`tuning_loc2`)**:
     - Asset: `"ST_TuningLoc2"`, Id: `"tuning_loc2"`, Name: `"Tuning"`, BasePrice: `60`, Duration: `18f`
     - Supply: `"tuning_parts"`, Capacity: `10`
     - Worker: `"Tuning Specialist"`, HireCost: `650`, RequiredLevel: `4`
     - XpReward: `25`
   - **Paint (`paint_loc2`)**:
     - Asset: `"ST_PaintLoc2"`, Id: `"paint_loc2"`, Name: `"Paint"`, BasePrice: `90`, Duration: `24f`
     - Supply: `"paint_cans"`, Capacity: `10`
     - Worker: `"Painter"`, HireCost: `800`, RequiredLevel: `5`
     - XpReward: `35`
3. В методе `FillServiceType(ServiceTypeConfig asset, ServiceTypeSpec spec)` сериализовать эти поля:
   ```csharp
   serialized.FindProperty("_supplyTypeId").stringValue = spec.SupplyTypeId;
   serialized.FindProperty("_supplyCapacity").intValue = spec.SupplyCapacity;
   serialized.FindProperty("_workerTitle").stringValue = spec.WorkerTitle;
   serialized.FindProperty("_workerHireCost").longValue = spec.WorkerHireCost;
   serialized.FindProperty("_workerRequiredLevel").intValue = spec.WorkerRequiredLevel;
   serialized.FindProperty("_xpReward").intValue = spec.XpReward;
   ```
4. Обновить массив `Buildables`:
   - `B_TravelToLoc2`: `"b_travel_to_loc2"`, `"Travel to Location 2"`, BuildableKind.TravelPoint, `"travel_to_loc2"`, Cost: `250`, RequiredLevel: `2`, FlowBonus: `0f`
   - `B_TravelToLoc1`: `"b_travel_to_loc1"`, `"Travel to Location 1"`, BuildableKind.TravelPoint, `"travel_to_loc1"`, Cost: `0`, RequiredLevel: `1`, FlowBonus: `0f`
   - `B_Loc2_Tires1`: `"loc2_build_tires_1"`, `"Tires 1"`, BuildableKind.ServicePoint, `"loc2_tires_1"`, Cost: `500`, RequiredLevel: `2`, FlowBonus: `0.2f`
   - `B_Loc2_Tuning1`: `"loc2_build_tuning_1"`, `"Tuning 1"`, BuildableKind.ServicePoint, `"loc2_tuning_1"`, Cost: `900`, RequiredLevel: `3`, FlowBonus: `0.2f`
   - `B_Loc2_Paint1`: `"loc2_build_paint_1"`, `"Paint 1"`, BuildableKind.ServicePoint, `"loc2_paint_1"`, Cost: `1400`, RequiredLevel: `4`, FlowBonus: `0.2f`
5. Автоматически регистрировать визуал коробок в `SupplyVisualCatalog.asset`:
   - Найти `SupplyVisualCatalog.asset` через `AssetDatabase.FindAssets("t:SupplyVisualCatalog")`.
   - Если найден, добавить недостающие записи:
     - `"tires_box"` -> цвет `new Color(0.2f, 0.2f, 0.22f, 1f)`
     - `"tuning_parts"` -> цвет `new Color(0.9f, 0.55f, 0.15f, 1f)`
     - `"paint_cans"` -> цвет `new Color(0.75f, 0.2f, 0.85f, 1f)`

### 1.2. Обновление `Assets/_Project/Scripts/Bootstrap/Editor/Location1ConfigCreator.cs`
В `Location1ConfigCreator.cs` настроить `_xpReward` для сервисов Локации 1, чтобы игрок мог прокачиваться до 2 уровня и открывать Локацию 2:
- В `PointStaff` (или `ServiceTypeSpec`) добавить XpReward:
  - `wash` -> `5` XP
  - `oil` -> `10` XP
  - `tires` -> `15` XP
- В `FillServiceType` / `CompleteStaffSupplies` заполнять `_xpReward` в ассетах `ST_Wash.asset`, `ST_Oil.asset`, `ST_Tires.asset`.

---

## 2. Доработка `Assets/_Project/Scripts/Bootstrap/Editor/WhiteboxLocation2Builder.cs`

1. **Теги и кольца Dwell для Travel Plot:**
   В методе `CreateTravelPlot`:
   - Создать тег на `ghost.transform` через `CreateTagCanvas`:
     ```csharp
     GhostTag ghostTag = CreateTagCanvas(ghost.transform, new Vector3(0f, 15f, 0f), "Travel");
     ```
   - Записать в `plotSo`:
     ```csharp
     plotSo.FindProperty("_ring").objectReferenceValue = ghostTag.Ring;
     plotSo.FindProperty("_priceTag").objectReferenceValue = ghostTag.Label;
     ```
   Это обеспечит отображение ценника и прогресс-круга над точкой перехода до ее постройки.

2. **Автоматическое запекание NavMesh:**
   В конце метода `BuildLocation2()`, сразу перед или после сохранения сцены, вызывать:
   ```csharp
   var problems = new List<string>();
   int baked = ModuleSetupA2.BakeNavMeshes(problems);
   Debug.Log($"[Whitebox] NavMesh baked: {baked} surface(s). Problems: {problems.Count}");
   ```
   Благодаря этому и игрок, и машины сразу получат рабочий NavMesh на Локации 2 без ручных манипуляций в инспекторе.

---

## 3. Подключение `ProgressionView` в сцену `Gameplay.unity`

1. В `Assets/_Project/Scripts/Bootstrap/Editor/ModuleSetupC1.cs` метод `EnsureProgressionView` уже реализован!
2. Добавить отдельный быстрый `MenuItem` для настройки только HUD прогрессии (без сброса всей сцены):
   ```csharp
   [MenuItem("AutoService/Setup/Setup Progression HUD")]
   public static void SetupProgressionHudOnly()
   {
       Scene scene = EditorSceneManager.GetActiveScene();
       if (scene.name != GameplaySceneName)
       {
           scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
       }

       var entryPoint = FindRoot(scene, EntryPointName);
       if (entryPoint == null)
       {
           Debug.LogError("[Setup] EntryPoint not found in Gameplay scene");
           return;
       }

       var entry = new SerializedObject(entryPoint);
       Transform hud = FindScreenHud(scene, entry.FindProperty("_balanceView").objectReferenceValue as BalanceView);
       if (hud == null)
       {
           Debug.LogError("[Setup] HUD Canvas not found");
           return;
       }

       var problems = new List<string>();
       ProgressionView progView = EnsureProgressionView(hud, problems);
       entry.FindProperty("_progressionView").objectReferenceValue = progView;
       entry.ApplyModifiedPropertiesWithoutUndo();

       EditorSceneManager.MarkSceneDirty(scene);
       EditorSceneManager.SaveScene(scene);
       Debug.Log("[Setup] ProgressionView successfully wired to GameplayEntryPoint in Gameplay.unity!");
   }
   ```
   Позиционирование `ProgressionView`:
   - В верхнем левом углу HUD, справа от кнопки паузы (кнопка паузы на X: 24, W: 72 -> ProgressionView на X: 110, Y: -24, W: 240, H: 60).
   - Включает уровень `LevelText` ("Lvl 1"), `XpText` ("0 / 15 XP"), и горизонтальную полосу `ProgressBarFill` (синий цвет).

---

## 4. Порядок проверки и тестирования

После внесения правок:
1. Выполнить пункт меню **`AutoService -> Whitebox -> Create Location 1 Configs`** (запишет XP награды для сервисов Локации 1).
2. Выполнить пункт меню **`AutoService -> Whitebox -> Create Location 2 Configs`** (создаст/обновит конфиги Локации 2, привяжет расходники `tires_box`, `tuning_parts`, `paint_cans`, цены, уровни, рабочих и цвета коробок).
3. Выполнить пункт меню **`AutoService -> Setup -> Setup Progression HUD`** (создаст и подключит плашку уровня/XP в сцене `Gameplay.unity`).
4. Выполнить пункт меню **`AutoService -> Whitebox -> Build Location 2 (and Travel Points)`** (соберет Локацию 2, площадки обслуживания, склад, зоны телепортации и автоматически запечет NavMesh).
5. Запустить игру в Play Mode:
   - Обслужить несколько машин на мойке Локации 1 -> проверить, что начисляется XP и уровень повышается до Lvl 2.
   - Построить точку телепорта `Travel to Location 2` на Локации 1.
   - Встать в круг телепортации -> игрок переносится на Локацию 2.
   - Купить бокс шиномонтажа `Tires 1` на Локации 2.
   - Подойти к складу Локации 2 -> взять коробку шин -> отнести в бокс шиномонтажа.
   - Встать на желтый квадрат `WorkPad` -> дождаться обслуживания машины -> убедиться, что прогресс бар заполняется, машина обслужена, получены деньги и XP.
   - Встать на синий круг `ManagePad` -> нанять работника `"Tire Specialist"`.
   - Проверить боксы тюнинга и покраски: замена модели на `Sport` и перекраска кузова.
   - Вернуться на Локацию 1 через точку перехода `Travel to Location 1`.
